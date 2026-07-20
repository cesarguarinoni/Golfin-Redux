# Self-Review — `club_bag_wedge_default` (iter-3, CESAR_REJECTED #2 follow-up)

> Written by `golfin-self-reviewer` — 2026-07-20 JST — iteration 3 (post-Cesar-rejection #2).
> Code + save-schema + bot-behaviour task, not a Figma/UI task — no Figma fidelity gate applies.
> Two rejections stand on this task: #1 (wedge shows driver icon — fixed in iter-2) and #2
> (capture had flipped frames from immediate-Arm boot-capture — iter-3 wired deferred-start).
>
> **Scope of this pass (per orchestrator):** feature correctness only. The frame-flip is a
> Cesar-acknowledged capture-tooling artifact that is OUT OF SCOPE for this verdict — it is
> not verified, not adjudicated, not counted for/against. It is carried as an open item the
> orchestrator tracks separately.

## Verdict

**PASS — FORWARD_TO_ARCHITECT (feature PASS; frame-flip carried as accepted open item)**

Rejection #1's wedge-portrait defect is still GONE on iter-3 (verified independently against
the fresh iter-3 canonical still, plus spot-checks on the driver and putter frames — all three
clubs render the correct portrait + yardage). The iter-2 BotDriver.cs fix is intact in the
iter-3 source. Hole 1 completed in 6 REAL strokes with `terminal=InCup` / `holed=real` and a
`ForceShotComplete` grep of 0 on the iter-3 `live_stat_log.txt`. The approved wedge-feature
files (ClubManager / SaveData / SaveSchemaMigrator / ClubOwnership / tests) are unchanged
between iter-2 and iter-3; iter-3's Physics/ edits are exactly the two capture-tooling files
called out by CESAR_REJECTION #2 (`Scenarios.cs` deferred wiring + `Editor/LoopV2SmokeBotMenu.cs`
menu item) — reusing the existing `Hole1Playthrough` scenario, no new `*Gate` scenario, no
physics-core touch.

## Step 1 — Visual scan (pixels first, spec last)

### `screenshots/iter3_s08_stroke5_2026-07-20_08-57-45.png` (1170×2532) — canonical rejection-follow-up

- Top-left HUD: JAMES portrait (Lv 10), TURN 5.
- Top-center flag readout: **9 yds** to the pin (bot is on/right next to the green).
- Top-right HUD: LOMOND / HOLE 1 - REGULAR / PAR 5 + green-plan mini-map with red flag.
- Ball on green with a short blue trajectory arc pointing up-left; power ring at 34% / 84.4 yd.
- Bottom-left column: SPIN button on top, then GOLFIN ball button with ∞.
- Bottom-right column: STRAIGHT button on top, then the load-bearing club-selection button.
- **Load-bearing pixel patch (bottom-right, club-selection button):** a red-and-silver club
  head with visible "SWING" text stamped on the face — clearly the Royal Swing wedge portrait,
  NOT the red-and-white G&F driver head. Label below reads "P. WEDGE" and "120 yrds". Label,
  portrait, and yards all agree on WEDGE.

### `screenshots/iter3_s04_stroke1_2026-07-20_08-56-46.png` — driver spot-check

- Tee shot through trees, "443 yds" to flag, TURN 2.
- Club button: red-and-white driver head, "DRIVER" + "250 yrds". Correct.

### `screenshots/iter3_s09_stroke6_2026-07-20_08-57-59.png` — putter spot-check

- On the green, 1 mts to flag, TURN 6, red putt line.
- Club button: silver-headed mallet-style putter portrait, "PUTTER" + "27 mts". Correct.

The three portraits are visually distinct and match their labels — the "driver icon on every
non-driver bot swing" class of bug is not present in iter-3.

## Step 2 — Rejection #1 code-fix verification

`Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` lines 779–786 (LIVE-path club-sync block):

```csharp
var entry = bag[bagIdx];
Golfin.Gameplay.UI.HUD.ClubContext.SelectedClubId    = entry.ClubId;
Golfin.Gameplay.UI.HUD.ClubContext.SelectedIndex     = bagIdx;
Golfin.Gameplay.UI.HUD.ClubContext.SelectedTypeLabel = entry.TypeLabel;
Golfin.Gameplay.UI.HUD.ClubContext.SelectedPortrait  = entry.Portrait;  // Order 761 fix ...
Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance  = entry.Distance;  // Order 761 fix ...
Golfin.Gameplay.UI.HUD.ClubContext.RaiseSelectedChanged();
```

Both missing assignments are present, both from `entry` (same source as SelectByIndex), both
before `RaiseSelectedChanged()`. Matches CESAR_REJECTION #1 §Fix exactly. Verdict on
Rejection #1: **GONE — RESOLVED**.

## Step 3 — Hole 1 ≤7 real strokes (Hard Gate 1)

Cited log evidence (independently confirmed by grep on the file):

```
$ grep -c ForceShotComplete tasks/loop_v2_smoke_bot/hole1_playthrough/live_stat_log.txt
0

$ grep 'PlayHoleToCup done\|terminal=InCup' tasks/loop_v2_smoke_bot/hole1_playthrough/live_stat_log.txt | tail -3
[t=105.05] [BotDriver]   Stroke 6 terminal=InCup endSurface=Green ball=(-231.4, 10.2, -72.4)
[t=107.03] [BotDriver] === PlayHoleToCup done: 6 strokes, holed=real ===
```

6 REAL strokes, InCup on stroke 6, `holed=real`, `ForceShotComplete` grep = 0. Under the ≤7
budget, no seam relied upon. **PASS.**

## Step 4 — Iter-3 code scope (Rejection #2 fix + standing bans)

`git diff HEAD --stat` (task-relevant rows only):

| File | Iter change | In-scope? |
|---|---|---|
| `Assets/Scripts/ClubManager.cs` | iter-1 (Changes 1/2/4) — unchanged in iter-3 | approved feature, no delta |
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | iter-1 (Change 5) + iter-2 (2-line HUD fix) — unchanged in iter-3 | approved feature, no delta |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | **iter-3** — deferred-record block added to existing `Hole1Playthrough` | Yes — sanctioned by CESAR_REJECTION #2 |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | **iter-3** — `RunHole1PlaythroughDeferred()` menu item | Yes — sanctioned by CESAR_REJECTION #2 |
| `Assets/Scripts/Save/ClubOwnership.cs` | iter-1 (Change 2) — unchanged in iter-3 | approved feature, no delta |
| `Assets/Scripts/Save/SaveData.cs` | iter-1 (Change 3) — unchanged in iter-3 | approved feature, no delta |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | iter-1 (Change 3) — unchanged in iter-3 | approved feature, no delta |
| `Assets/Scripts/Save/Tests/*.cs` | iter-1 (tests + v8→v9 assertions) — unchanged in iter-3 | approved feature, no delta |

**Standing bans (rule 7 + CESAR_REJECTION #2 scope):**

- `git diff HEAD -- Assets/Scripts/Physics/` = exactly 3 files: `BotDriver.cs`, `Scenarios.cs`,
  `Editor/LoopV2SmokeBotMenu.cs`. All permitted (feature file + capture-tooling files).
- `Scenarios.cs` diff (verified by reading the hunk): the added block sits **inside the
  existing `Hole1Playthrough()` method** as a step-5b insert (`WaitForSceneLoaded("Hole_01_Geo")`
  → 4s settle → guarded `BeginDeferred` via reflection → 1s settle → existing `d.Capture`
  path continues). It is guarded by `SessionState.GetBool("LoopV2SmokeBot.DeferredRecord")`
  so the plain (non-recording) menu path is a full no-op. **No new `*Gate` scenario, no new
  scenario at all** — reuses the existing `Hole1Playthrough`. Rule 7 clean.
- No physics-core file touched (nothing under `Assets/Scripts/Physics/Physics/`,
  `Ballistics/`, `Surfaces/`, `Solver/`, etc. — only the bot/scenario/menu wiring layer).
- No `M_Splash*.mat`, no `LabScaffold.unity`, no `ShellScene.unity` diff.

**Approved-feature integrity:** the seven previously-approved files (ClubManager, SaveData,
SaveSchemaMigrator, ClubOwnership, three test files) are unchanged in the iter-3 diff — the
`--stat` line counts are the same as iter-1's shipped totals and no `Scenarios.cs`-style block
was inserted into any of them. iter-3 is strictly capture-tooling on top of an already-passed
feature.

## Step 5 — Iter-3 out-of-scope item (carried, NOT adjudicated)

- **Frame-flip in the capture window** — per orchestrator's scoping ruling, Cesar has viewed
  the clip, confirmed a flip is present, and explicitly ruled that this self-review is not
  to verify or re-adjudicate it. It is neither PASS nor FAIL for this pass; it is an accepted
  capture-tooling artifact the orchestrator tracks separately. The `iter3_flipcheck_*` tiles
  and the implementer's "zero flips across 165 sampled frames" claim were NOT opened, NOT
  re-verified, and are explicitly bypassed per the scoping ruling. The `record_start_realtime`
  offset showing recording started ~25s in (well after boot) is noted but not weighted — the
  flip dimension is outside this verdict entirely.

## Step 6 — Capture-helper compliance

- Bot-recorded video via the sanctioned `BotVideoRecorder` deferred-start path (mirrors the
  existing `AudioGameplayShotsV3` / `AudioPuttToCup` pattern). The mid-play stills used for
  club-button verification are frame-extracts from the same real-flow bot run (bot navigated
  via real `GameplaySceneLoader.BeginGameplayLoad` → `Hole_01_Geo`), which satisfies the
  standing "real flow" rule. No `ScreenCapture.CaptureScreenshot` path, no per-task workaround.
- No new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` in this iter, so
  the CaptureHelper maintenance protocol is not triggered.

## Fail list

None. Feature is correct; both rejection root causes have code fixes in place; Hole 1
completes cleanly at ≤7 real strokes; scope is clean per SPEC and CESAR_REJECTION #2.

## Status transition

`STATUS.md` → `SELF_REVIEW_PASS` (routing to `golfin-reviewer` for the second gate).

## Files touched by this review

| Path | Change |
|---|---|
| `Docs/Specs/Active/club_bag_wedge_default/SELF_REVIEW.md` | Overwrote iter-2 review with iter-3 PASS verdict |
| `Docs/Specs/Active/club_bag_wedge_default/STATUS.md` | `READY_FOR_SELF_REVIEW` → `SELF_REVIEW_PASS` |
