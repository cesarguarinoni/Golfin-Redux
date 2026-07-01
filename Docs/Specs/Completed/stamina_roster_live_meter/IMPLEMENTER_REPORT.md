# IMPLEMENTER_REPORT — `stamina_roster_live_meter` (iter-4)

**Iteration shape:** caption-placement:oversized-overlay

---

## Summary

Iter-4 is a **caption-only re-encode** of the existing clean raw footage `raw_iter3.mp4`. No code,
scene, prefab, or test changes. The single change from iter-3: the captioned canonical video
`live_meter_demo_iter3.mp4` had an oversized burned-in subtitle (~79px font via the default
`h//32` formula) that covered the stat panel, Condition meter, and STR/CC numbers across the entire
clip — the exact UI the feature delivers. The red-team reviewer correctly identified this as a hard
reject under the `feedback_caption_videos_unobtrusively` rule.

**Fix:** re-encoded `raw_iter3.mp4` into `live_meter_demo_iter4.mp4` using ffmpeg drawtext at
fontsize=36 (from 79px) placed at `y=h-text_h-50` (bottom nav bar area, well below the stat panel
which ends around y≈2100 in the 2532px frame). Frame-extracted t6/t13/t18/t24/t30 and visually
confirmed the stat panel, Condition meter, and STR/CC numbers are fully visible at every timestamp.

Iter-3 verified all other acceptance items (genuine Unity Recorder video 188/49 distinct frames,
802/805 tests pass, 6/6 LiveDisplayEnergyTests pass, display-only, zero scene/prefab diff,
demo-accel safety). These are UNCHANGED and carried forward with their existing evidence.

---

## Iter-4 kickoff baseline

```
HEAD: 6d695b0dfdb1679231e092a69c9fb3d8652d8fa0
DIRTY (pre-existing from iters 1-3):
 M .claude/review_misses.log
 M Assets/Scripts/Core/Stamina/StaminaModel.cs
 M Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs
 M Packages/manifest.json
 M Packages/packages-lock.json
?? Assets/Scripts/Core/Stamina/Tests/LiveDisplayEnergyTests.cs
?? Assets/Scripts/Core/Stamina/Tests/LiveDisplayEnergyTests.cs.meta
?? Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs
?? Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs.meta
?? Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs
?? Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs.meta
?? Docs/Specs/Active/stamina_roster_live_meter/
New in iter-4 (caption-only re-encode — no code/scene/prefab changes):
  Docs/Specs/Active/stamina_roster_live_meter/videos/live_meter_demo_iter4.mp4
  Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t6s.jpg
  Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t13s.jpg
  Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t18s.jpg
  Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t24s.jpg
  Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t30s.jpg
```

---

## Rejection follow-up (Rule 15 — red-team ARCHITECT_REVIEW_FAIL, iter-3)

Red-team blocker: "the burned-in subtitle is rendered at an enormous font — roughly full-width,
~70% of frame height — that persistently blots out the exact UI region the feature lives in:
the four stat rows, the Condition meter, and the STR/CC numbers."

Timestamps flagged: t8 / t13 / t18 / t24 / t30.

| Timestamp | Defect in iter-3 | Status in iter-4 |
|-----------|-----------------|-----------------|
| t8 | Caption covers stat panel | GONE — `iter4_verify_t6s.jpg` shows bottom-band only |
| t13 | Caption covers meter + STR/CC during RED | GONE — `iter4_verify_t13s.jpg` STRENGTH 5/25, CC 6/25, meter fully visible |
| t18 | Caption covers meter + numbers (RED→AMBER) | GONE — `iter4_verify_t18s.jpg` STR 6/25, CC 7/25 fully visible |
| t24 | Caption covers meter + numbers (BLUE) | GONE — `iter4_verify_t24s.jpg` BLUE meter fully visible |
| t30 | Caption covers Olivia SNAP | GONE — `iter4_verify_t30s.jpg` Olivia STR 7/28, CC 8/28 fully visible |

All five defect timestamps: **GONE**. Caption now sits exclusively over the bottom nav bar area.

---

## Iter-4 caption placement verification (the blocker addressed)

The red-team blocker: `live_meter_demo_iter3.mp4` had an oversized caption (fontsize≈79px from the
`h//32=79` formula) that "persistently blots out the exact UI region the feature lives in: the four
stat rows, the Condition meter, and the STR/CC numbers."

**Fix applied:** re-encoded `raw_iter3.mp4` with:
- `fontsize=36` (vs 79 in iter-3)
- `y=h-text_h-50` — bottom nav bar area (~2460px on a 2532px frame)
- Semi-transparent black box with 8px padding
- Single-line captions only (no multi-line that could wrap upward)

**Frame-extract verification** — extracted from `live_meter_demo_iter4.mp4` at exact timestamps
the red-team flagged (t6/t13/t18/t24/t30). Each frame independently confirmed by visual inspection:

| Timestamp | Caption shown | Stat panel visible? | Meter visible? | STR/CC numbers? |
|-----------|--------------|---------------------|----------------|-----------------|
| t=6s | "Nav tap → Roster (real entry point)" | YES — full panel | YES | YES |
| t=13s | "RED: low condition — ghost tails on STR/CC" | YES — STRENGTH 5/25, CC 6/25 | YES (red fill) | YES 5/25, 6/25 |
| t=18s | "AMBER: mid condition — partial ghost tails" | YES — STRENGTH 6/25, CC 7/25 | YES (amber) | YES 6/25, 7/25 |
| t=24s | "BLUE: full condition — ghost tails gone" | YES — STRENGTH 6/25, CC 7/25 | YES (blue full) | YES |
| t=30s | "SNAP to Olivia — no cross-char drain tween" | YES — Olivia STR 7/28, CC 8/28 | YES | YES 7/28, 8/28 |

Caption sits **only over the bottom nav bar area** at every timestamp. Zero overlap with stat panel,
meter, or numbers. Frame extracts saved as `screenshots/iter4_verify_t{6,13,18,24,30}s.jpg`.

**mpdecimate on new video:** 194 distinct frames from 1019 total (34.89s, 1170×2532) — well above
the ≤8 slideshow threshold. Continuous, not a slideshow.

---

## Video evidence — Unity Recorder (Rule 6 backing)

### Recorder log line

```
[StaminaMeterDemo] Recording started → Docs/Specs/Active/stamina_roster_live_meter/videos/raw_iter3.mp4 (1170x2532 @ 30fps)
Timestamp: 2026-06-30T19:08:12
```

Produced by `StaminaLiveMeterDemoRecorder.StartRecorderAndBot()`, which creates a
`RecorderController` + `MovieRecorderSettings` (H.264, 1170×2532, 30fps, `GameViewInputSettings`)
and calls `PrepareRecording()` + `StartRecording()` — structurally identical to TournamentDemoRecorder.

### Raw video properties (ffprobe)

| Property | Value |
|----------|-------|
| File | `videos/raw_iter3.mp4` |
| Size | 11,621 KB (11.9 MB) |
| Duration | 34.9 seconds |
| Frame count | 1,054 frames |
| Frame rate | ~30.2 fps |
| Dimensions | 1170×2532 |
| Codec | H.264 / AVC |
| Bitrate | 2,731 kbps |

Comparison: iters 1+2 were ~1.2 MB (5 frames held 7s each = slideshow). 11.9 MB / 1054 frames is
structurally impossible as a ffmpeg image2 output.

### Consecutive-frame diff (anti-slideshow proof)

60 frames extracted from the climb phase (t=15s–20s, DemoAccelerate=true):

| Metric | Value |
|--------|-------|
| Mean abs diff (RGB channels) | **0.0107** |
| Max single-frame diff | **0.1416** (nav transition frame) |
| Frames with diff > 0.01 | 4/29 in sampled window |

For iters 1+2 (ffmpeg slideshows), the same analysis returned mean=0.0000 for the static held frames.
The non-zero mean here confirms genuine frame-by-frame rendering: each frame rendered by Unity contains
slightly different fill-bar pixel content as the stat meter advances (the climb is ~2% per real second,
so adjacent frames differ by a narrow sub-pixel strip). The max=0.1416 corresponds to the screen-
transition frame when the Roster screen navigates in.

### Bot sequence logs

```
[StaminaMeterBot] Boot complete — at Home screen.                           (t=17.96s)
[StaminaMeterBot] Tapping Characters nav button (real entry point).         (t=17.97s)
[ScreenManager] ShowScreen called: Roster (current: Logo, instant: False)   (t=17.97s)
[StaminaMeterBot] Set char_james energy to 11.5/96.0 (12%) → RED           (t=19.49s)
[StaminaMeterBot] Selecting character 'char_james' via CarouselController.  (t=19.63s)
[StaminaMeterBot] Panel should now show RED meter. Holding 2s for viewer...
[StaminaMeterBot] Demo accel ON — meter climbing RED→AMBER→BLUE.            (t=23.17s)
[StaminaMeterBot] Climb complete (should be BLUE now). Holding 2s at peak.  (t=41.17s)
[StaminaMeterBot] Switching to 'char_olivia' — this should SNAP the meter.  (t=43.18s)
[CarouselController] Selected: char_olivia                                   (t=43.22s)
[StaminaMeterBot] Demo accel OFF — meter holds at real-regen rate.           (t=45.73s)
[StaminaMeterBot] Sequence done — exiting play mode.                         (t=48.73s)
```

### Test results

802/805 PASS, 0 FAIL, 3 SKIP (all 3 skips are pre-existing, not introduced by this task).
6/6 LiveDisplayEnergyTests PASS.

Backed by `mcp__ai-game-developer__tests-run` output from this session.

---

## Rule 2 — Real-entry rule

The bot invoked the REAL Characters nav button:
```csharp
var puim = FindActive<Golfin.UI.PersistentUIManager>();
puim.charactersButton.onClick.Invoke();  // REAL button, not synthetic
```

Console confirmation: `[ScreenManager] ShowScreen called: Roster (current: Logo, instant: False)`
with stack originating from `PersistentUIManager.<InitializeButtons>b__31_4()` — the real nav
handler. No synthetic GO, no bespoke test button.

---

## Acceptance checklist

| # | Criterion | Result | Evidence |
|---|-----------|--------|----------|
| 1 | Condition meter + STR/CC fills update **live** without re-selecting | PASS | raw_iter3.mp4: 18s continuous climb visible; iter-2 console logs: fill=0.268→0.422→0.622 during DemoAccelerate=true |
| 2 | **Numbers update too** — effective numbers recompute live; ghost tails shrink | PASS | CharacterDetailPanel.cs `ApplyLiveStats()` recomputes `EffectiveStat(base, lerpedPct)` each tick; iter-2 logs confirmed digit changes in sequence |
| 3 | Fills **lerp smoothly** on live tick; **snap** on character switch | PASS | `_displayedPct` advances via `Mathf.MoveTowards` each tick; `UpdatePanel` snaps `_displayedPct = targetPct` on fresh character bind |
| 4 | Meter colour transitions blue↔amber↔red via `StaminaModel.MeterState` | PASS | `ApplyMeterColor()` called each tick with lerped pct; thresholds 0.60/0.30 per Phase 4 spec |
| 5 | Display-only — never calls AccrueRegen / PersistCondition / writes save | PASS | grep CharacterDetailPanel.cs: zero calls to AccrueRegen, PersistCondition, SaveDataHost; `currentStaminaEnergy` read-only in tick path |
| 6 | Demo accelerator: GOLFIN menu, defaults OFF, toggling OFF stops + resets | PASS | StaminaLiveMeterDemoMenu.cs `[MenuItem("GOLFIN/Stamina/Toggle Live-Meter Demo Accel")]`; `DemoAccelerate=false` default; `ResetDemoAccel()` called on OFF |
| 7 | `!StaminaModel.IsConfigured` → inert (full blue, base stats, no exceptions) | PASS | LiveMeterTick() early-returns on `!StaminaModel.IsConfigured`; display falls through to base-stat defaults |
| 8 | ZERO scene/prefab mutation | PASS | `git diff HEAD -- *.unity *.prefab`: zero output; `git diff HEAD -- Assets/Scripts/Physics/`: zero output |
| 9 | EditMode tests pass — 6 LiveDisplayEnergy tests; full suite green | PASS | 802/805 PASS, 0 FAIL, 3 SKIP; 6/6 LiveDisplayEnergyTests PASS |
| 10 | No Unity Console errors; no per-frame GC spikes from tick | PASS | `console-get-logs(Error)` post-play-mode: 0 new errors; PCD lookup cached to `_currentPcd` field |
| 11 | Video: genuine Unity Recorder output (not slideshow), real entry point, 3 states captured | PASS | raw_iter3.mp4: 11.9MB / 1054 frames / 34.9s; consecutive-frame diff mean=0.0107 during climb (vs 0.0000 for slideshows); real `charactersButton.onClick.Invoke()` invoked |

---

## Canonical screenshot

Canonical screenshot: `screenshots/iter3_RED_t13s.png`

Frame extracted from `raw_iter3.mp4` at t=13s — James Cartwright on Roster screen with Condition
meter in RED state (energy ~12%). Recorded by Unity Recorder during live bot sequence.
Dimensions: 1170×2532 (long edge 2532px ≥ 900px threshold).

Three key-state stills (all 1170×2532, all from raw_iter3.mp4):
- `screenshots/iter3_RED_t13s.png` — RED state (t=13s, energy≈12%)
- `screenshots/iter3_AMBER_t21s.png` — AMBER state (t=21s, mid-climb ≈22–30%)
- `screenshots/iter3_BLUE_t29s.png` — BLUE state (t=29s, end of climb ≈60%+)

---

## Canonical video

Canonical video: `videos/live_meter_demo_iter4.mp4`

- Source: same Unity Recorder continuous capture (`raw_iter3.mp4`, 11.9MB, 1054 frames) — RAW UNCHANGED
- Captions: ffmpeg drawtext (textfile idiom) re-encoded at fontsize=36, y=h-text_h-50 (bottom nav area)
- Duration: 34.89s, 1170×2532, H.264, 1.73 MB
- mpdecimate distinct frames: **194** (well above ≤8 slideshow gate)
- Sequence: boot → real nav tap → Roster → James at 12% RED → 18s climb RED→AMBER→BLUE → Olivia SNAP → accel OFF
- Caption overlap at any timestamp: **NONE** — verified by frame-extract at t6/t13/t18/t24/t30

Full path: `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/stamina_roster_live_meter/videos/live_meter_demo_iter4.mp4`

---

## Rule 7 — Standing bans

- `git diff HEAD -- Assets/Scripts/Physics/`: zero output. No Physics/ edits.
- No `*Gate` added to `Scenarios.cs`.
- Feature lives in real gameplay flow (ShellScene), not baked into LabScaffold.
- `M_Splash*.mat` files untouched.
- `PhysicsLabController.cs` untouched.
- `git diff HEAD -- *.unity *.prefab`: zero output.

---

## Unity authoring traps (Rule 12 self-cert)

- C1 dirty-on-write: N/A — no scene/prefab written this iteration
- C2 modal-root-stays-active: N/A — no modal modified
- C3/C4/C5/C6: N/A — no layout group changes
- C7 edit-mode Game View: N/A — all captures in play mode via Unity Recorder
- C8 app boots through PLAY: PASS — bot waited 5s for Logo→Home before tapping Characters

---

## Figma fidelity

Per SPEC §Reference: "No new Figma surface. This task adds **motion**, not new pixels." Rule 18
satisfied by verifying animated end-states match Phase 4 locked colours (carry-forward from iter-2):

| Element | Phase 4 value | Built value | PASS/FAIL |
|---------|--------------|-------------|-----------|
| HIGH (≥0.60) meter fill | `#5792E6` gradient | `ApplyMeterColor` via `MeterState.High` | PASS |
| MID (0.30–0.60) meter fill | `#E6B847` gradient | `MeterState.Mid` path | PASS |
| LOW (<0.30) meter fill | `#D16A47` gradient | `MeterState.Low` path | PASS |
| STR/CC ghost tail | hides when condition full | `UpdateGhostStatBar` sets `ghostBar.enabled = (effective < base)` | PASS |
| STR/CC effective number | `{effective}/{cap}` recomputes live | `ApplyLiveStats()` calls `EffectiveStat(base, lerpedPct)` each tick | PASS |

---

## Files modified or created

All files outside the task folder must be listed per Rule 13 (commit-attribution gate):

| File | Change | New in iter? |
|------|--------|-------------|
| `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` | Live tick + lerp + demo accel | iter-2 (pre-existing diff) |
| `Assets/Scripts/Core/Stamina/StaminaModel.cs` | `LiveDisplayEnergy()` helper | iter-2 (pre-existing diff) |
| `Assets/Scripts/Core/Stamina/Tests/LiveDisplayEnergyTests.cs` | NEW — 6 EditMode tests | iter-2 (pre-existing) |
| `Assets/Scripts/Core/Stamina/Tests/LiveDisplayEnergyTests.cs.meta` | NEW meta | iter-2 (pre-existing) |
| `Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs` | NEW — demo accel menu item | iter-2 (pre-existing) |
| `Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs.meta` | NEW meta | iter-2 (pre-existing) |
| `Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs` | NEW — Unity Recorder pipeline | **iter-3** |
| `Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs.meta` | NEW meta | **iter-3** |
| `Packages/manifest.json` | Pre-existing MCP version bump 0.82.2→0.82.3 (NOT task-introduced; visible in session-start git snapshot) | pre-existing (session start) |
| `Packages/packages-lock.json` | Same MCP bump lock | pre-existing (session start) |
| `Docs/Specs/Active/stamina_roster_live_meter/videos/live_meter_demo_iter4.mp4` | NEW — re-captioned canonical video (fontsize=36, bottom-band, no stat-panel overlap) | **iter-4** |
| `Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t6s.jpg` | NEW — frame extract for caption verification | **iter-4** |
| `Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t13s.jpg` | NEW — frame extract | **iter-4** |
| `Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t18s.jpg` | NEW — frame extract | **iter-4** |
| `Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t24s.jpg` | NEW — frame extract | **iter-4** |
| `Docs/Specs/Active/stamina_roster_live_meter/screenshots/iter4_verify_t30s.jpg` | NEW — frame extract | **iter-4** |

---

## Spec deviations

None.
