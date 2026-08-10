# Implementer Report — `power_gauge_target_marker`

## Implementation summary

The landing target the player places in map view now survives the map session as a **radial
notch on the power gauge**, at the % of club carry that would land the shot there. The seam is a
single new `ShotController.MapTargetCarryM` (metres, `-1` = none) written back in
`MapViewController.CloseImmediate()` beside the existing yaw write-back, read by
`PowerGaugeWidget` and drawn procedurally by `PowerGaugeGraphic`. **The power system is
untouched** — no `ComputePower`, overpower, `ControlsConfig` or perfect-zone change; this is a
readout, not a recalibration. The verified §3.2 gap is also closed: nothing had ever called
`PowerGaugeWidget.SetMaxCarryYards()`, so the yards text sat on its 250f default; the widget now
resolves carry from `ClubContext.SelectedDistance` live.

**Iteration shape:** `shotui:map-target-marker`

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | modified (+21) — new `MapTargetCarryM` property (metres, default −1) + reset at `CommitFlick` |
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | modified (+12 in `CloseImmediate`) — write `_aimedCarryM` back as `MapTargetCarryM` alongside the yaw write-back |
| `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs` | modified (+76/−14) — `MarkerFrac01` / `MarkerUnreachable`, notch quad drawn after the fill, radius-parameterised `AddQuad` |
| `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` | modified (+70/−1) — pure `ComputeMarkerFrac` seam, `ResolveCarryYards()` from `ClubContext`, per-update marker push, Yards-only gate |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | modified (+8 in `ExitPutterMode`) — seed `SetMaxCarryYards` from `ClubContext.SelectedDistance` (§3.2 wiring fix) |
| `Assets/Scripts/Gameplay/Tests/PowerGaugeMarkerTests.cs` | **created** (+196) — 11 EditMode tests: `ComputeMarkerFrac` math + `MapTargetCarryM` lifecycle |
| `Assets/Scripts/UI/Editor/PowerGaugeMarkerVerifyBot.cs` | **created** — editor-only acceptance harness (real entry path, 6 frames + `marker_invariants.json`). Beyond the SPEC's file list — see § Spec deviations |
| `Assets/Scripts/UI/Editor/PowerGaugeMarkerDemoRecorder.cs` | **created** — editor-only demo clip for the daily report. Sequence + captions ONLY; recording goes through `BotVideoRecorder` (see § Recording method) |
| `Docs/Specs/Active/power_gauge_target_marker/marker_invariants.json` | created — 23/23 PASS gate output |

**No scene edits, no prefab edits, no new art.** The notch is vertices in the existing
`PowerGaugeGraphic` mesh; the new serialized fields fall back to their C# defaults on the
existing scene instance (`LabScaffold.unity` untouched — verified via `git status`).

## Screenshot

- **Canonical screenshot:** `screenshots/B_notch_at_mapped_target.png` (1170×2532)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` → gameplay load → Hole 1
- **Play mode:** Yes
- **Hole loaded:** Hole 1 (LOMOND, Par 5)
- **Entry path:** ShellScene boot → `StartButton` → `PlayButton` → Hole 1 card `actionButton` →
  real `HoleMap` button `onClick` → production `TrySetAimFromScreenPoint` → real map `SHOOT`
  button `onClick`. No synthetic/test-only button anywhere in the chain (PIPELINE_HARDENING §2).

| Frame | Shows |
|---|---|
| `screenshots/A_no_target_no_notch.png` | Tee, no map session yet — clean gauge, no notch |
| `screenshots/B_map_target_placed.png` | Map open with the landing placed at 91.75 m |
| `screenshots/B_notch_at_mapped_target.png` | **Canonical** — notch at 40.1% (0.4014 × 360° = 144.5°), "150.0 yd" |
| `screenshots/C_notch_after_club_change.png` | Same target, Wood 230 yd — notch moved to 43.6%, text now "138.0 yd" |
| `screenshots/D_no_notch_after_shot.png` | TURN 2, 462 yds to flag — notch gone, marker not stale |
| `screenshots/E_putter_mode_no_notch.png` | Meters mode ("15.0 mts") with a 40 m target forced — no notch |

## Video (sign-off artifact)

`videos/power_gauge_target_marker_marker.mp4` — 1170×2532, 51.4s, captioned. Recorded through
`BotVideoRecorder` (see § Recording method) on the real entry path: tee with a clean gauge → open
the hole map → place the landing → SHOOT → the notch appears at that % → pull to the notch →
swap club (notch moves, yards text corrects 155.0 → 142.6) → flick → next stroke markerless.
Copied to `Docs/Reports/Media/2026-08-10_power_gauge_target_marker.mp4` for the daily report.

Orientation verified from decoded frames (upright HUD, no Y-flip). Closing frame verified to read
TURN 2 / 463 yds with a notch-free gauge — see the defect note in § Recording method.

## Invariant JSON (the gate — PIPELINE_HARDENING §3)

`marker_invariants.json` — **23 assertions, 0 fail, verdict PASS**. Produced by
`PowerGaugeMarkerVerifyBot` in one live play-mode session on Hole 1. Every capture assertion
verifies the PNG **exists on disk and is ≥900px** before citing it (the first run reported
6 × `capture MISSING` — `SnapPlayModeSafe` hands back a path for a file it never wrote when the
editor is unfocused; the harness caught it rather than citing phantom frames, and the run was
repeated with Unity focused).

## Recording method (corrected after Cesar's challenge)

**First attempt was wrong and was rewritten.** `PowerGaugeMarkerDemoRecorder` originally
hand-rolled its own `RecorderController` / `MovieRecorderSettings` / Game-View sizing — 137
identical code lines copied from `MapViewStrictCropDemoRecorder` (44% of the file), duplicating
an engine that already exists. Cesar caught it: *"Why did you build a recorder? There is already
an approved recording method."*

Rewritten onto the sanctioned engine, `Golfin.Physics.Viewer.Editor.BotVideoRecorder`, using the
same contract as `TournamentLoopCaptureHarness` and the OB/zone capture menus:

| Concern | Now owned by |
|---|---|
| iPhone-14 1170×2532 Game-View pinning + fabricated-entry purge | `BotVideoRecorder` |
| Full-res output, `RecorderController` lifecycle | `BotVideoRecorder` |
| Y-flip render-state lock (vSync/targetFrameRate before `StartRecording`) | `BotVideoRecorder` |
| `CaptureCore.RecordingActive` lock | `BotVideoRecorder` |
| `record_info.json` (the caption clock) | `BotVideoRecorder` |
| Duration watchdog + one-clip-per-session GPU guard | `BotVideoRecorder` |
| **The marker sequence + `Step()` captions** | **this file (the only thing left)** |

Contract used: `ResetSessionGuard()` → `CustomOutputPath` → `MaxRecordSecondsSessionOverride`
(120s; the default 30s watchdog would truncate this ~75s clip) → `ArmDeferred()` before play mode,
`BeginDeferred()` once the hole is stable, and `End()` deliberately **not** called here because
`LoopV2SmokeBotMenu.ExitingPlayMode` calls it unconditionally (one `End()` per session is the
documented contract). No `Scenarios.cs` entry was added (standing ban) — `LoopV2SmokeBot.Scenario`
is set only so `record_info.json` lands beside `history.log` for `build_bot_video.py`.

### The first cut of the clip was not shippable — caught and fixed

The initial recording ended with the caption *"One marker per shot — gone until you map again"*
burned over a frame still reading **TURN 1** with the ball on the tee and the notch still drawn:
**the flick never fired.** Cause: `TickArrow` auto-cancels a swing after
`ControlsConfig.MaxTotalPasses` (10 passes ≈ 8s at low ClubControl). The demo held one continuous
pull for ~9.4s across two segments, so `ShotController` had already returned to Idle and
`EndExternalDrag` was a no-op — silently.

Fixed two ways: (a) every pull segment is now short and ends in `CancelExternalDrag` (which resets
the arrow clock), and the flick is its own fresh ~2s pull released immediately; (b) the runner now
**asserts the commit** — it reads `MapTargetCarryM` after release and `Debug.LogError`s
"FLICK DID NOT COMMIT … This clip is NOT shippable" if the shot didn't happen, so a lying caption
can't be produced silently again. Re-recorded: log reads `Flick committed (MapTargetCarryM
cleared)` and `post-shot MarkerFrac01=-1.000`, and the closing frame reads **TURN 2, 463 yds** with
a notch-free gauge.

Also fixed: captions were clipping off both frame edges (`build_bot_video.py` uses `fontsize =
h/32` ≈ 79px, so ~30 chars fit on a 1170px frame). Shortened and hand-wrapped in the recorder
source, ≤26 chars per line.

Still duplicated and NOT fixed: the ~35 lines of real-widget click helpers
(`FindButton`/`ClickReal`/`ClickWhenPresent`/`ClickHoleCard`), which every one of the 13
`*DemoRecorder` files carries its own copy of. Extracting a shared helper is a cross-cutting
refactor of committed files, out of scope here — flagging it rather than doing it silently.

## Acceptance checklist (SPEC §5)

### EditMode (`PowerGaugeMarkerTests`, 11 tests)

| Item | Result | Justification |
|---|---|---|
| No target (−1) → no marker | PASS | `NoTarget_YieldsNoMarker`: `ComputeMarkerFrac(-1, 200)` returns `MarkerNone` (−1), `unreachable=false` |
| Target == carry → 1.0 | PASS | `TargetEqualsClubCarry_IsFullPower`: 182.88 m / 200 yd → 1.0000 ±0.0005 |
| Half carry → 0.5 | PASS | `TargetAtHalfCarry_IsHalfPower`: 91.44 m / 200 yd → 0.5000 |
| Beyond 1.2 → pinned + over-reach flag | PASS | `TargetBeyondOverpower_...`: 1.8× carry → frac pinned at 1.2, `unreachable=true` |
| Club change re-derives the fraction | PASS | `ClubChange_MovesMarker_...`: 160 m constant → 0.6998 (250 yd) / 1.0936 (160 yd) / 1.2+red (90 yd wedge) |
| Committed shot → `MapTargetCarryM == -1` | PASS | `CommittedShot_ClearsMapTarget`: BeginExternalDrag→SetExternalPower→EndExternalDrag; `OnShotResolved` fired, target = −1 |
| (extra) Fumbled flick keeps the target | PASS | `FumbledFlick_KeepsMapTarget`: `CompleteShot()`→`TransitionToIdle` leaves 137.5 m intact |
| (extra) Zero/negative carry → no marker | PASS | `ZeroOrNegativeCarry_YieldsNoMarker`: guards the divide when `ClubContext` is unpopulated |
| (extra) Overpower band 1.1× not flagged | PASS | `TargetInOverpowerBand_...`: 1.1 returned, `unreachable=false` |
| (extra) Near-zero target clamps to floor | PASS | `VeryNearTarget_ClampsToVisibleFloor`: 0.5 m / 250 yd → 0.02, not a 0° sliver |
| (extra) Default is markerless | PASS | `MapTargetCarryM_DefaultsToNoTarget`: fresh `ShotController` = −1 |

**Full EditMode suite: 1089 total / 1086 passed / 0 failed / 3 skipped** (the 3 skips are
pre-existing `HoleCompleteDriverTests` Stage-C1 skips, present at baseline `ce5f47a86`).
The 11 new tests were additionally run individually (11/11 PASS) because the MCP `tests-run`
class filter does not narrow the run.

### Editor manual matrix (SPEC §5, live on Hole 1)

| Item | Result | Justification |
|---|---|---|
| Map a target → close → notch at the matching % | PASS | `_aimedCarryM` = 91.75 m → `MapTargetCarryM` = 91.75 m (exact) → `MarkerFrac01` = 0.4014 vs expected 0.4014 (91.75 / 228.6 m). Visible as a white notch at ~144° in `B_notch_at_mapped_target.png` |
| Change club → notch moves | PASS | Driver 250 yd → Wood 230 yd with the target unchanged at 91.75 m: frac 0.4014 → 0.4363. Both frames captured; the notch is visibly further clockwise in `C_...png` |
| Shoot → notch gone next stroke | PASS | `MapTargetCarryM` = −1 immediately after the committed flick; on the next stroke `MarkerFrac01` = −1 and `D_no_notch_after_shot.png` shows TURN 2 with a clean gauge |
| Putter → no notch | PASS | With `MapTargetCarryM` forced to 40 m and the widget in Meters mode, `MarkerFrac01` = −1; `E_...png` reads "15.0 mts" with no notch |
| Yards text matches the selected club (§3.2 fix) | PASS | With Wood 230 yd at 60% power the gauge reads **138.0 yd**. Before this change the same frame read **150.0 yd** (250f default × 0.6). See § Yards text before/after |
| Pull the flick to the notch → ball lands ≈ the map target | **NOT VERIFIED** | See § Known FAIL / not verified |

## Yards text — before / after (SPEC §3.2)

| | Club in hand | `_maxCarryYards` | Gauge text @ 60% |
|---|---|---|---|
| **Before** | any | `250f` (serialized default; **zero callers** of `SetMaxCarryYards`) | `150.0 yd` always |
| **After** | Driver 250 yd | resolved live from `ClubContext.SelectedDistance` | `150.0 yd` |
| **After** | Wood 230 yd | resolved live from `ClubContext.SelectedDistance` | `138.0 yd` |

The Driver row is **not discriminating** (250 happens to equal the old default) — that is stated
explicitly in the JSON as `B.yards_text_tracks_club … NOT discriminating when club==250yd`. The
discriminating measurement is `C.yards_text_discriminating` after the club change.

## Known FAIL / not verified

1. **"Flick to the notch → the ball lands on the map target" is NOT verified.** The harness holds
   the gauge at a fixed 60% to photograph the notch; it never flicks *to* the notch and measures
   the resulting carry. Honest scope statement: this task guarantees the notch sits at
   `target / clubCarry`, i.e. it is **exactly as truthful as `ClubContext.SelectedDistance` is**.
   Which is P-006 — see below. Needs an on-device / manual pass or a follow-up carry-vs-marker bot.
2. **P-006 evidence — no 100%-flick measurement taken.** SPEC §6 asks for any observed
   100%-flick vs `SelectedDistance` mismatch. The session fired one shot at 60% power, not 100%,
   so **no P-006 data point was produced**. Not a blocker for the readout, but the requested
   evidence is absent and should not be inferred from this run.
3. **Touch input is not exercised.** The Editor has no `Touchscreen`, so the map target was placed
   through the production `TrySetAimFromScreenPoint` (the exact call the finger drives) rather than
   through a real finger, and the gauge was held via the `BeginExternalDrag`/`SetExternalPower`
   path (what `ClubHandleDragger` calls). The gesture plumbing above those calls is unchanged by
   this task but is untested here.
4. **Notch legibility at small power values** is unverified below ~5% — the 0.02 floor guarantees
   a drawable quad but the 2.5°-wide notch at the 12-o'clock start overlaps the arc origin.

## Needs on-device verification

- The notch's readability at phone size and in sunlight (white @ 0.95 alpha, 2.5° wide,
  ±4px overhang) — it reads clearly at 1170×2532 in the Editor, but that is a desktop monitor.
- The unreachable/red state (`frac > 1.2`) was proven in EditMode math only; no live frame shows
  a red pinned notch because reaching it needs a target placed past 1.2× carry on a real map.
- Real touch: place a target with a finger, close, flick, confirm the marker reads honestly.

## Spec deviations

- **New file `Assets/Scripts/UI/Editor/PowerGaugeMarkerVerifyBot.cs`** — the SPEC lists four
  production files plus tests. Getting SPEC §5's "editor manual matrix + screenshots" honestly
  (real entry path, per-assertion PASS/FAIL) needs a play-mode harness; hand-driving it via
  `script-execute` would have meant a non-reproducible one-off and a banned hand-rolled capture
  path. Modelled on the existing `*DemoRecorder` family. Editor-only (`#if UNITY_EDITOR`), no
  production reference to it. Flagging rather than assuming.
- **The §3.2 wiring is primarily widget-side, not `PhysicsLabController`-side.** The SPEC says to
  wire max carry at `PhysicsLabController` L546 (`ExitPutterMode`). That call was added — but
  `ExitPutterMode` only fires on a club *change*, never at boot, so on its own it would have left
  the yards text on 250f for the whole first hole. `PowerGaugeWidget.ResolveCarryYards()` reads
  `ClubContext.SelectedDistance` live, which is what actually closes the gap; the
  `PhysicsLabController` line remains as a seed for contexts where the bus is unpopulated.
- **§3.4 optional `→ NN%` second line on `_pctText`: SKIPPED.** `_pctText` is a single-line TMP
  centred in the gauge hub with "150.0 yd" already directly beneath it; adding a third line needs
  layout surgery, which §3.4 explicitly says to skip and flag.
- **Reset site (SPEC §3.1 leaves the choice to the implementer): `CommitFlick`, not
  `TransitionToIdle`.** `TransitionToIdle` is also the failed-flick / arrow-timeout / cancelled-drag
  path — resetting there would delete the marker the player just placed without a shot ever being
  taken. Covered by `FumbledFlick_KeepsMapTarget`.

## Console output

No errors or warnings attributable to this task. `PowerGaugeGraphic.OnPopulateMesh` logs one
pre-existing `Debug.Log` per mesh rebuild (present before this change; the marker fraction was
appended to the existing line, no new log added).

Diagnostic lines added by this task (informational, one per map close):

```
[MapView v2] Close write-back: MapTargetCarryM=91.8m (aimedCarry=91.8m, clubCarry=228.6m)
```

## Baseline / attribution

Task baseline `ce5f47a86`. **A parallel session committed this work mid-flight** — the four
production files + the new test landed in `9047d6444 feat(shotui): power_gauge_target_marker — WIP
checkpoint` and `ec219da71` (`PhysicsLabController`, swept into the `aim_camera_ball_centering`
commit) without my involvement. HEAD is now `ed3198dcd`. Content was re-verified against HEAD
after the fact. Still uncommitted: `PowerGaugeMarkerVerifyBot.cs` (+ `.meta`) and
`marker_invariants.json`. `screenshots/` is gitignored by convention
(`.gitignore:246 Docs/Specs/**/screenshots/`), not drift. No other working-tree drift.

## Open questions for Architect

1. **P-006 gating (SPEC §6).** The marker is only as honest as `SelectedDistance ≈ actual carry at
   100%`. This run produced no measurement of that. Worth scheduling the carry-vs-marker
   measurement pass before Cesar reads the notch as a promise?
2. **Unreachable state has no live frame.** Should the harness be extended to place a deliberately
   out-of-reach target (wedge + far landing) so the red pinned notch gets a real screenshot?
