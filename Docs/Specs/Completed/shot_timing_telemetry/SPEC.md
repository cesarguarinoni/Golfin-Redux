# SPEC — `shot_timing_telemetry`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-29 by the Architect (Cowork). Follow-up to `shot_timing_power` (F15, `4210c0891`): Cesar — "add it to analytics".

## Goal

Every `shot_taken` telemetry event carries the flick timing the player actually hit (`timing01`, the slab progress sampled at the aim latch) and the power multiplier it produced (`timing_mul`), and the admin dashboard's **Shot Quality** card ("do the controls work", `telemetry-panel.tsx`) shows how testers are timing their flicks. This is the data that decides the F15 tuning (0.70 / 0.90, band edges) and the D5 putt question — without it those stay guesses.

## Where the code stands (verified 2026-08-29)

- `ShotController` (`Golfin.Gameplay.Input`) exposes `LastTimingPowerMul` (set in `CommitFlick`, survives until the next commit) and `LastTimingAtLatch => _timingAtLatch` — but `_timingAtLatch` is cleared by `ResetSwingSamples()` from `TransitionToIdle()`, which `CompleteShot()` calls when the ball rests. **It is not safe to read at shot-complete time.**
- `shot_taken` is emitted in `TelemetryHooks.OnHistoryChanged` (`Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs:~297`) from the last `GameSession.ShotRecord` — a post-resolution record built by `HoleSessionDriver.BuildShotRecord(ShotResult)` (`Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs:~100`) on `BallStateMachine.OnShotComplete`. `HoleSessionDriver` holds `PhysicsLabController controller`; `PhysicsLabController` holds `[SerializeField] ShotController _shotController` (private, line 61). `Golfin.Physics.Viewer.asmdef` already references `Golfin.Gameplay.Input` and `Golfin.Gameplay.Loop`.
- `ShotRecord` (`GameSession.cs:214`) is a readonly struct with a 9-arg ctor (+ an 8-arg forwarder); `HoleSessionDriver.BuildShotRecordStatic` test seams exist (2 overloads); 3 test files construct `ShotRecord` directly.
- Backend `playlife/backend/routers/telemetry.py` stores `payload` as a free JSON dict (size-capped) — **no backend change**.
- Dashboard: `Tools/admin-dashboard/lib/telemetryData.ts::buildShotQuality` aggregates `shot_taken`; `lib/types.ts::ShotQuality`; `app/(panels)/telemetry/telemetry-panel.tsx` renders the cards (~line 479); `lib/i18n.ts` carries `tel.shots.*` strings (EN + JP); `lib/mockTelemetry.ts` fakes rows for mock mode.

## Decisions

- **D1 — snapshot at commit, not at complete.** `ShotController` gets `public float LastCommittedTiming01 { get; private set; } = float.NaN;` set in `CommitFlick` next to `LastTimingPowerMul` (from `_timingAtLatch`, NaN when the swing was sampleless). Neither is touched by `ResetSwingSamples`/`TransitionToIdle`, so they are still the *last shot's* values when `OnShotComplete` fires. No new events, no relay change.
- **D2 — ride the existing record.** `ShotRecord` gains `float Timing01` (NaN = no touch swing) and `float TimingPowerMul` (1.0 default) via a new 11-arg ctor; the 9- and 8-arg ctors forward with `(float.NaN, 1f)`. Existing constructions and tests compile unchanged.
- **D3 — payload keys.** `timing01`: number rounded to 2 dp, or **null** when NaN (bots / sampleless — never send a fake 0, which would read as a red flick). `timing_mul`: number, 2 dp. `timing_band`: `"green" | "gold" | "red" | null`, derived client-side from the same `ControlsConfig.Default.TimingBandGoldY01/GreenY01` edges the shot used — so the dashboard never has to know the edges.
- **D4 — dashboard shows the distribution, not a new panel.** `ShotQuality` gains `timingSampled`, `timingGreenRate`, `timingGoldRate`, `timingRedRate` (each ÷ `timingSampled`, null when 0) and `avgTimingMul`. One new card row in the Shot Quality section: "Flick timing" — green / gold / red % with `timingSampled` as the sub-label, plus avg multiplier. Amber when red share > 0.40 (testers can't hit the window → widen `TimingBandGreenY01`). Mock rows get the three keys.
- **D5 — §23 applies.** The dashboard change is real only when deployed: `npm run deploy`, quote the Cloudflare deployment id, and read the sidebar commit stamp off `admin.golfin.world` in the report.

## Implementation

### Phase A — Unity

1. `ShotController.cs`: `LastCommittedTiming01` property; in `CommitFlick`, alongside `LastTimingPowerMul = timingMul;` add `LastCommittedTiming01 = _timingAtLatch;`. The band derivation stays OUT of `Golfin.Gameplay.Input` (step 4).
2. `PhysicsLabController.cs`: `internal ShotController ShotController => _shotController;` (same assembly as `HoleSessionDriver`).
3. `HoleSessionDriver.BuildShotRecord`: read `controller?.ShotController` → `timing01 = sc != null ? sc.LastCommittedTiming01 : float.NaN`, `mul = sc != null ? sc.LastTimingPowerMul : 1f`; pass into the new ctor. NOTE: `controller` can be null in some scaffolds (the driver already guards it) — keep the fallback.
4. `GameSession.ShotRecord`: fields + 11-arg ctor; forwarders. Add a static helper `GameSession.TimingBand(float timing01)` returning `"green"/"gold"/"red"/null` from `ControlsConfig.Default` (`Golfin.Gameplay.Loop` → check it references `Golfin.Gameplay.Config`; if not, add the asmdef reference — it is a leaf assembly with no UI dependency). NOTE: if that reference is undesirable, put the helper in `TelemetryHooks` instead and say so.
5. `TelemetryHooks.OnHistoryChanged`: add
   `["timing01"] = float.IsNaN(shot.Timing01) ? null : (object)Math.Round(shot.Timing01, 2)`,
   `["timing_mul"] = Math.Round(shot.TimingPowerMul, 2)`,
   `["timing_band"] = GameSession.TimingBand(shot.Timing01)`.
   Confirm the serializer accepts `null` values (the existing `ob_reason` is already nullable — same path).
6. Tests (`Assets/Scripts/Gameplay/Tests/` or the existing telemetry test file — match where `OnHistoryChanged` is covered today):
   - `ShotRecord_ForwardingCtors_DefaultTiming` — 8/9-arg ctors give NaN / 1.0.
   - `TimingBand_EdgesMatchConfig` — 0.84 → gold, 0.85 → green, 0.44 → red, NaN → null.
   - `ShotTaken_Payload_CarriesTiming` — record with (0.9, 1.0) → payload has `timing01 0.9`, `timing_band "green"`; record with NaN → `timing01 null`, `timing_band null`.
   - `ShotController_LastCommittedTiming01_SurvivesCompleteShot` — latch at 0.3, End (commit), `CompleteShot()` → property still 0.3 (and `LastTimingAtLatch` is NaN — the point of D1).

### Phase B — Dashboard (`Tools/admin-dashboard/`)

7. `lib/types.ts`: extend `ShotQuality` (D4 fields).
8. `lib/telemetryData.ts::buildShotQuality`: count `timing_band` values (prefer the band key; if absent but `timing01` is a number, do NOT re-derive — count as unsampled; the client owns the edges), mean of `timing_mul` over sampled rows.
9. `app/(panels)/telemetry/telemetry-panel.tsx`: one card row after the OB card; `pct()` helper as the neighbours; amber rule per D4.
10. `lib/i18n.ts`: `tel.shots.timing`, `tel.shots.timingSub`, `tel.shots.timingMul` — EN + JP (JP: フリックのタイミング / 計測ショット数 / 平均パワー倍率).
11. `lib/mockTelemetry.ts`: `shot_taken` mock rows get `timing01`/`timing_mul`/`timing_band` (mix of the three bands, some null).
12. `npm run build` green → `npm run deploy` → quote the deployment id (§23).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Phase A tests pass; whole `Golfin.Gameplay.Tests` assembly green, no filter; existing `ShotRecord` tests untouched and green.
- [ ] Editor play, Hole 01, telemetry enabled (`GOLFIN_TESTBUILD` / whatever gate `beta_telemetry` uses — state it): one green flick and one red flick → the two `shot_taken` events show `timing_band` "green" / "red" and `timing_mul` 1.0 / ~0.7 (quote the event JSON from the telemetry log or the dashboard explorer).
- [ ] One bot/`FireDebugShot` shot → `timing01 null`, `timing_band null`, `timing_mul 1`.
- [ ] Dashboard mock mode renders the Flick timing card with non-zero shares.
- [ ] §21 live E2E: the two Editor shots above appear on **admin.golfin.world** Telemetry → Shot Quality with the sampled count incremented (screenshot).
- [ ] §23: `npm run deploy` output with the Cloudflare deployment id quoted, and the sidebar commit stamp on the live site matches HEAD.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged with justification.

## Out of scope

- Any change to the F15 numbers or the D5 putt decision — this task produces the data for those, it does not make them.
- New telemetry events, relay changes, backend/schema changes.
- Per-tester or per-club timing breakdowns (add later if the aggregate says something).

## Files this task touches

- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `LastCommittedTiming01`.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — internal accessor; `HoleSessionDriver.cs` — read + pass through.
- `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — `ShotRecord` fields/ctor, `TimingBand` helper (+ asmdef ref if needed).
- `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` — three payload keys.
- Tests as §6.
- `Tools/admin-dashboard/lib/{types,telemetryData,i18n,mockTelemetry}.ts`, `app/(panels)/telemetry/telemetry-panel.tsx`.
- `Docs/AI_CONTEXT.md`; `Docs/Specs/Completed/beta_telemetry/SPEC.md` is history — instead add the three keys to whichever doc lists the `shot_taken` payload (`Docs/Specs/Active/telemetry_admin_panel/` or `Docs/ADMIN_DASHBOARD_OPS.md` — find it, don't guess).

## Smoke evidence

EditMode summary, the two live event JSONs, the live dashboard screenshot, the deployment id.
