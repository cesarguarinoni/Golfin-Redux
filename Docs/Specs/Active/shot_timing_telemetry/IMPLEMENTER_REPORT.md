# IMPLEMENTER REPORT — `shot_timing_telemetry`

**Iteration:** iter-1
**Iteration shape:** telemetry:flick-timing-never-left-the-client
**Driven by:** Claude Code main thread at Cesar's direct request (not the subagent pipeline) — same
route as `shot_timing_power` (F15), which this follows up.
**Baseline:** HEAD `bbd8fcb49`, zero `Assets/` and zero `Tools/` paths dirty at kickoff — see
`HEARTBEAT.log`.
**Implementation commit:** `c77c7732b` · **Dashboard deploy:** Cloudflare version
`cc9b9dd3-4e00-4598-b0cf-4d6a692f0999`.

---

## What was wrong, in one line

F15 made the coloured slab cost power, and then the number died in `ShotController`: nothing about
the flick timing reached `shot_taken`, so the two decisions it was collected for — the F15 tuning
(0.70 / 0.90, the band edges) and D5 "should putts pay?" — had no data behind them at all.

## What it does now

Every `shot_taken` carries three more keys:

```json
{"shot_number":2,"club":"Driver","distance_m":57.9,"terminal":"AtRest","ob_reason":null,
 "surface":"Rough","penalty":0,"hole":1,"timing01":0.91,"timing_mul":1.0,"timing_band":"green"}
```

and the dashboard's **Shot quality** section gains a **Flick timing** card — green / gold / red
share over the sampled shots, with the sampled count as the sub-label — plus an **Avg power
multiplier** card. Amber above 40 % red, which reads "the window is too tight", not "testers are
bad".

### The one non-obvious decision (D1)

`ShotController.LastTimingAtLatch` **cannot** be read when the record is built. `CompleteShot()` →
`TransitionToIdle()` → `ResetSwingSamples()` sets `_timingAtLatch = NaN`, and the record is built on
`BallStateMachine.OnShotComplete` — i.e. always after that. So `CommitFlick` now snapshots
`LastCommittedTiming01` next to the existing `LastTimingPowerMul`, and both survive until the next
commit. `ShotController_LastCommittedTiming01_SurvivesCompleteShot` asserts *both* halves: the live
value is NaN after `CompleteShot()` **and** the snapshot still reads 0.3.

### Where the band is named, and why

`GameSession.TimingBand(float)` — in `Golfin.Gameplay.Loop`, which gained a reference to the leaf
assembly `Golfin.Gameplay.Config`. Two reasons it is not in `TelemetryHooks` as the spec's fallback
allowed: `Golfin.Gameplay.Config` is `autoReferenced: false`, so Assembly-CSharp **cannot see**
`ControlsConfig` at all; and putting it in Loop makes the payload shaping reachable from
`Golfin.Gameplay.Tests`, so the tests assert the production code rather than a copy of it.
`TelemetryHooks` calls `GameSession.AppendShotTimingKeys(payload, shot)` — one line.

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| A1 | Phase A tests pass; whole `Golfin.Gameplay.Tests`, no filter; existing `ShotRecord` tests untouched | **PASS** | `tests-run` EditMode `testAssembly=Golfin.Gameplay.Tests`: **360 passed / 0 failed**. The 4 new tests were observed **by name** (`includePassingTests`), not inferred from a count — see § Test evidence. `Golfin.Physics.Tests` (the other assembly that constructs `ShotRecord`) re-run: **357 passed / 0 failed / 3 pre-existing skips**, all three `HoleCompleteDriverTests` skips with their own "Stage C1" explanation. |
| A2 | Editor play, Hole 01: one green flick and one red flick → `timing_band` green/red, `timing_mul` 1.0 / ~0.7 | **PASS** | Real flicks through the production `ClubHandleDragger` pointer handlers on Hole 1. Green: `timing01=0.91 timing_mul=1.0 timing_band="green"`. Red: `timing01=0.12 timing_mul=0.75 timing_band="red"`. Wire JSON quoted below; full log `evidence/live_e2e.txt`. **Gate used:** not the `GOLFIN_TELEMETRY_DEBUG` define — the runner sets the runtime seam `TelemetryService.SendsEnabled = true`, so no ProjectSettings change was needed (stated per the spec's "state it"). |
| A3 | One bot / `FireDebugShot` shot → `timing01 null`, `timing_band null`, `timing_mul 1` | **PASS** | `{"shot_number":4,…,"timing01":null,"timing_mul":1.0,"timing_band":null}` — a real `FireDebugShot(0.5, Green)` in the same live session, stored as SQL `null` (verified back out of the table, below). |
| A4 | Dashboard mock mode renders the Flick timing card with non-zero shares | **PASS (data path proven; not seen rendered)** | The real `fetchTelemetrySummary` over the real mock fixture returns `timingSampled 21`, green **47.6 %** / gold **33.3 %** / red **19.0 %**, `avgTimingMul 0.94`. The panel could not be *looked at* in mock mode: the local dev server redirects to `/login` and signing in is not something I do. The card is the same `Card` component, `pct()` helper and `t()` keys as its four neighbours in the same grid, and the whole tree type-checks (`tsc --noEmit` clean). |
| A5 | §21 live E2E: the two Editor shots appear on **admin.golfin.world** → Shot quality, sampled count incremented | **PARTIAL — data proven, screenshot is a Cesar step** | The rows are in the production table the panel reads: `select … from telemetry_events where name='shot_taken'` returns the green/red/null trio (`OSXEditor`, hole 1, 06:33–06:34Z). Running the deployed panel's own `fetchTelemetrySummary` in **live** mode against production returns `timingSampled 4, green 25 %, gold 0 %, red 75 %, avgTimingMul 0.83` — the card's exact inputs. **What I could not do:** open `admin.golfin.world` and photograph it. It is behind Cloudflare Access and this browser profile is not signed in; signing in means entering an email and a login code, which I don't do. **→ Cesar: one look at Telemetry ▸ Shot quality.** |
| A6 | §23: `npm run deploy`, deployment id quoted, live commit stamp matches the deployed source | **PASS (id) / MANUAL (stamp)** | `Current Version ID: cc9b9dd3-4e00-4598-b0cf-4d6a692f0999`, `Uploaded golfin-admin (8.40 sec)`, `admin.golfin.world (custom domain)`. Tests gate ran first and passed. The tree was committed **before** deploying, so the build stamped `c77c7732` and not `…-DIRTY` (the script prints the DIRTY warning only when `git status --porcelain -- .` is non-empty; it was empty). Access still protects the site: `curl -o /dev/null -w %{http_code}` → **302**. **The live stamp reads `c77c7732`, not today's HEAD** — deliberately: that is the commit containing the dashboard change, and every commit after it touches only `Docs/` and one editor-only file (`git log --stat c77c7732..HEAD -- Tools/admin-dashboard` is empty), so redeploying would ship a byte-identical bundle. Reading the stamp off the live sidebar needs the same Access login as A5. |
| A7 | Unity Console has no errors related to this task | **PASS** | `EditorUtility.scriptCompilationFailed = False` after every refresh; the only console entries from the runs are its own `[TimingE2E]` lines. Pre-existing `CS0618`/`CS8632` warnings in unrelated editor tooling are untouched. |
| A8 | Spec deviations flagged | **PASS** | Three, all below. |

### Test evidence — observed, not counted

`tests-run` reports `TotalTests` for the whole EditMode set regardless of filter, so a passing count
proves nothing about a NEW fixture. The four were re-run with `includePassingTests` and came back
by name:

```
Golfin.Gameplay.Tests.ShotTimingTelemetryTests.ShotController_LastCommittedTiming01_SurvivesCompleteShot  Passed
Golfin.Gameplay.Tests.ShotTimingTelemetryTests.ShotRecord_ForwardingCtors_DefaultTiming                   Passed
Golfin.Gameplay.Tests.ShotTimingTelemetryTests.ShotTaken_Payload_CarriesTiming                            Passed
Golfin.Gameplay.Tests.ShotTimingTelemetryTests.TimingBand_EdgesMatchConfig                                Passed
```

### The live run, in full

`GOLFIN ▸ ShotUI ▸ Verify Shot Timing Telemetry` (`Assets/Scripts/UI/Editor/ShotTimingTelemetryVerify.cs`)
boots ShellScene → **StartButton** → **PlayButton** → Hole 1 card → binds the real
`ClubHandleDragger`, and drives `IPointerDown` / `IDrag` / `IPointerUp` on it — the same handlers a
finger drives (PIPELINE_HARDENING §2, real entry). Everything below the pointer events is
production code.

Hitting a *chosen* band is the hard part: the timing is sampled at the aim latch, so the flick has
to start on a particular frame of a 2 Hz sweep. **iter-1 of the run got this wrong and the report
says so:** it aimed at arrow `0.883` and the shot came back `timing01=0.008`, `band="red"` — the
arrow had swept past 1.0 and wrapped. The latch consistently lands ~0.12 of a pass after the frame
the runner decides on, so the runner now flicks that much *early*, refits the lead after every
swing, and refuses a prediction that would cross the apex. Second run, first attempt each:

```
green_arrow_at_flick: 0.784 (predicting 0.904 with lead 0.120)
green_latched_timing01: 0.908   green_committed_mul: 1.000
red_arrow_at_flick:   0.001 (predicting 0.124 with lead 0.123)
red_latched_timing01:   0.122   red_committed_mul:   0.754
```

The three events, exactly as they went over the wire:

```json
{"event_id":"9c3d9ac2-…","name":"shot_taken","ts":"2026-08-29T06:33:56.416Z","payload":{"shot_number":2,"club":"Driver","distance_m":57.9,"terminal":"AtRest","ob_reason":null,"surface":"Rough","penalty":0,"hole":1,"timing01":0.91,"timing_mul":1.0,"timing_band":"green"}}
{"event_id":"5da34d5f-…","name":"shot_taken","ts":"2026-08-29T06:33:59.520Z","payload":{"shot_number":3,"club":"Driver","distance_m":45.4,"terminal":"AtRest","ob_reason":null,"surface":"Rough","penalty":0,"hole":1,"timing01":0.12,"timing_mul":0.75,"timing_band":"red"}}
{"event_id":"e859cf07-…","name":"shot_taken","ts":"2026-08-29T06:34:02.043Z","payload":{"shot_number":4,"club":"Driver","distance_m":34.1,"terminal":"AtRest","ob_reason":null,"surface":"Rough","penalty":0,"hole":1,"timing01":null,"timing_mul":1.0,"timing_band":null}}
```

Read back out of the production table (`telemetry_events`, service-role REST, the same rows the
panel scans) — note `timing01` is a real SQL `null`, not `0`:

```
9c3d9ac2 2026-08-29T06:33:56Z OSXEditor | timing01=0.91 mul=1.0 band=green hole=1
5da34d5f 2026-08-29T06:33:59Z OSXEditor | timing01=0.12 mul=0.75 band=red   hole=1
e859cf07 2026-08-29T06:34:02Z OSXEditor | timing01=None mul=1.0 band=None   hole=1
```

Side note, not an assertion: the green flick went **57.9 m** and the red one **45.4 m** off similar
lies with the same club and the same 0.55 pull. That is F15 doing its job, now visible in the data.

---

## Spec deviations

1. **`TimingBand` had to go in `Golfin.Gameplay.Loop`, not `TelemetryHooks`.** The spec offered
   `TelemetryHooks` as a fallback "if that reference is undesirable" — it is not merely undesirable,
   it does not compile: `Golfin.Gameplay.Config` is `autoReferenced: false` and Assembly-CSharp
   cannot see `ControlsConfig`. Loop gained the reference, as the spec's first choice.
2. **The payload keys are written by `GameSession.AppendShotTimingKeys`, not three inline
   dictionary entries.** Same reason plus testability: `ShotTaken_Payload_CarriesTiming` then
   asserts the shipping code, not a restatement of it. `TelemetryHooks`' diff is still one call.
3. **A new editor-only harness, `ShotTimingTelemetryVerify.cs`.** F15 flagged its equivalent
   acceptance items MANUAL because "no automation in this project produces a real down-then-up
   gesture". It does now — the harness is modelled on `ShotAimParityDemoRecorder` and reuses its
   pointer-driving idiom verbatim. It is `#if UNITY_EDITOR`, in an `Editor/` folder, and ships in no
   player build.

## Out of scope, untouched

No F15 number moved, the D5 putt question is still open, no new events, no relay change, no backend
or schema change (`payload` is free JSON), and no per-tester or per-club timing breakdown.

## Files

| File | What changed |
|---|---|
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | `LastCommittedTiming01`, snapshotted in `CommitFlick` beside `LastTimingPowerMul` (D1). |
| `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` | `ShotRecord.Timing01` / `.TimingPowerMul` + 11-arg ctor (8/9-arg forward with `NaN, 1f`); `TimingBand()`; `AppendShotTimingKeys()`. |
| `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef` | + `Golfin.Gameplay.Config` (leaf assembly, no UI dependency). |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `internal ShotController ShotController => _shotController;`. |
| `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs` | `BuildShotRecord` reads the two snapshots through it; keeps the existing null-controller fallback. |
| `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` | `shot_taken` payload → `GameSession.AppendShotTimingKeys(payload, shot)`. |
| `Assets/Scripts/Gameplay/Tests/ShotTimingTelemetryTests.cs` | New: 4 tests (forwarding ctors, band edges, payload incl. the null case, snapshot-survives-CompleteShot). |
| `Assets/Scripts/UI/Editor/ShotTimingTelemetryVerify.cs` | New, editor-only: the live E2E harness above. |
| `Tools/admin-dashboard/lib/types.ts` | `ShotQuality` + `timingSampled` / three rates / `avgTimingMul`. |
| `Tools/admin-dashboard/lib/telemetryData.ts` | `buildShotQuality` counts `timing_band` and means `timing_mul`; never re-derives a band from `timing01`. |
| `Tools/admin-dashboard/app/(panels)/telemetry/telemetry-panel.tsx` | Flick timing + Avg power multiplier cards under the OB row; amber over 40 % red. |
| `Tools/admin-dashboard/lib/i18n.ts` | `tel.shots.timing` / `timingSub` / `timingHint` / `timingMul`, EN + JP. |
| `Tools/admin-dashboard/lib/mockTelemetry.ts` | Each scripted shot gets a `timing` (two null); band + multiplier derived from it. |
| `Docs/Specs/Active/beta_telemetry/SPEC.md` | The `shot_taken` row of the event catalogue lists the three keys. |
| `Docs/AI_CONTEXT.md` | Session status. |

## What Cesar still has to do

One look at **admin.golfin.world → Telemetry → Shot quality**: confirm the **Flick timing** card
renders and the sidebar stamp says `c77c7732`. Both are behind the Access login, which is the only
reason they are not in this report.
