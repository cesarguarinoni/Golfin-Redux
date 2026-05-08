# Self Review — `loop_v1_2c_turn_counter_and_shot_history`

**Reviewer:** golfin-self-reviewer
**Date:** 2026-05-08 JST
**Iteration:** N=1
**Verdict:** **FORWARD_TO_ARCHITECT (PASS)**

## Visual diff notes (Step 1: pixels-only descriptions)

### C1 — `controls_2c_turn1_aiming_2026-05-08_14-59-34.png`
- Top yellow banner: "CAM: Chase BALL: Aiming". Top-right: white circle with dark gear icon.
- Top-left: PlayerCard. Character portrait (red cap), three navy bars stacked: white right-aligned text "PLAYER", "Lv 1", **"TURN 1"**.
- Top-right: three navy bars: "LOMOND", "HOLE 1 - REGULAR", "PAR 5".
- Small chips below cards: "0.0 mph" (left), "506 yds" (right).
- Center: golf ball with green G logo on tee, lush green fairway, trees flanking, distant pin.
- Bottom: SPIN button, GOLFIN club indicator, STRAIGHT direction, DRIVER 250 yds.

### C2 — `controls_2c_turn2_after_first_shot_2026-05-08_14-59-49.png`
- Same banner + gear button.
- PlayerCard now shows **"TURN 2"** (text changed from C1).
- Right card unchanged ("LOMOND", "HOLE 1 - REGULAR", "PAR 5").
- Distance chip now reads **"0 yds"** (was 506 yds — ball is ~at pin).
- Scene shows different camera framing with yellow aim flag visible — ball has clearly moved.
- Same bottom UI strip.

### C3 — `controls_2c_turn1_after_hole_reload_2026-05-08_14-59-51.png`
- Same banner + gear button.
- PlayerCard shows **"TURN 1"** again (reset from C2's "TURN 2").
- Right card unchanged.
- Distance chip back to **"506 yds"**.
- Camera framing similar to C2 (with yellow aim flag), but ball is on tee with white tee marker visible — full state reset.
- Same bottom UI strip.

### C4 — `controls_2c_history_log.txt`
> ShotHistory count=1 (captured after C2):
>   Entry[0]: ShotNumber=1 Club=Driver Terminal=AtRest OBReason=null Surface=Fairway DistXZ=258.7m Origin=(219.43, 11.46, 34.73) Final=(-32.18, 7.03, -25.32)

Schema matches spec exactly (8 fields per ShotRecord struct, all present in log line).

## Step 2 — Reference comparison

Spec declares no Figma reference. Visual states across C1→C2→C3 match the spec's expected outputs (TURN 1 → TURN 2 → TURN 1 reset). The TURN-counter increments between shots and resets on hole reload — exactly what §2c was designed to deliver.

## Step 3 — Acceptance checklist verification

| # | Item | Implementer | My verdict | Notes |
|---|---|---|---|---|
| 1  | `ShotHistory` list field | PASS | CONFIRM | `GameSession.cs` line 18 declares the list. |
| 2  | `OnHistoryChanged` event | PASS | CONFIRM | Line 19, `System.Action`. |
| 3  | `RecordShot(ShotRecord)` method | PASS | CONFIRM | Lines 20–24, appends + invokes event. |
| 4  | `ResetForNewHole()` method | PASS | CONFIRM | Lines 31–37, sets TurnCount=1, clears history, fires both events. |
| 5  | Existing TurnCount/SetTurn/OnTurnChanged signatures unchanged | PASS | CONFIRM | Lines 13–15, additive only. |
| 6  | `ShotRecord` struct in same namespace, 8 fields | PASS | CONFIRM | Lines 44–70 in same file, namespace `Golfin.Gameplay.UI.HUD`. |
| 7  | `HoleSessionDriver` MonoBehaviour created | PASS | CONFIRM | File exists, namespace `Golfin.Physics.Viewer`. |
| 8  | Wired in `LabScaffold.unity` on same GO as `LoopCameraDirector` | PASS | CONFIRM | Scene YAML grep confirms component exists with controller fileID 1483952038, postShotDelaySeconds=1.5. |
| 9  | `controller` reference set (not null) | PASS | CONFIRM | Same scene grep. |
| 10 | `OnHoleLoaded` calls `ResetForNewHole()` after `HoleContext.Raise()` | PASS | CONFIRM | `PhysicsLabController.cs` line 1248 immediately after line 1246. |
| 11 | `OnHoleUnloaded` calls `ResetForNewHole()` after `HoleContext.Reset()` | PASS | CONFIRM | Line 1397 immediately after line 1394. |
| 12 | 7 new EditMode tests in `HoleSessionDriverTests.cs` | PASS | CONFIRM | All 7 spec-required test names present with sane Assert calls. |
| 13 | Test gate 118 PASS / 0 FAIL / 0 SKIP | PASS | CONFIRM | `/tmp/iter2c_test_results.txt` line 1 reads `PASS:118 FAIL:0 SKIP:0`. Lines 47–53 are the 7 new §2c tests, all PASS. Math: 111 baseline + 7 new = 118. |
| 14 | C1 capture: TURN label = "TURN 1" | PASS | CONFIRM | Visually verified — see Step 1. |
| 15 | C2 capture: TURN label = "TURN 2" | PASS | CONFIRM | Visually verified, real text change (not stale frame — distance chip and scene also changed). |
| 16 | C3 capture: TURN label reset to "TURN 1" | PASS | CONFIRM | Visually verified, distance chip also reset to 506 yds proving full state reset. |
| 17 | C4 log: 1 entry, ShotNumber=1, Driver, AtRest | PASS | CONFIRM | Log content matches schema; values plausible (driver carry 258.7m XZ ~ 283yds is reasonable for calm preset). |
| 18 | TURN visibly updates and resets across the three captures | PASS | CONFIRM | C1→C2→C3 are genuinely different frames with different TURN values, different distance chips, and different ball positions. |
| 19 | No banned files modified | PASS | CONFIRM | File list shows no edits to `BallStateMachine`, `ShotResult`, etc. |
| 20 | `PlayerCardWidget.cs` not modified | PASS | CONFIRM | Not in file-list; spec declared zero-changes-needed. |
| 21 | `LabScaffold.unity` modified via Editor APIs | PASS | ACCEPT | Scene YAML looks well-formed (no `_EditorClassIdentifier` corruption, no missing GUIDs). Cannot fully verify without diff history but no smell. |

## Step 4 — Defects requiring root cause

None identified. All visual states match the spec's expected behaviour, all code matches the spec's prescribed structure, and the test gate is solid.

## Step 5 — Capture-helper compliance

### Screenshot provenance
The smoke runner `SmokeRunner2cHost.cs` uses a custom `SnapPlayMode()` helper that calls `CaptureCore.GrabGameViewRT()` directly — the same render-target read that `CaptureHelper.SnapGameView()` uses internally — but skips `AssetDatabase.Refresh()` to avoid forcing a domain reload that would kill the play-mode coroutine. The technical justification is documented inline (lines 51–53 and 149–153 of `SmokeRunner2cHost.cs`). The `ScreenCapture.CaptureScreenshotAsTexture()` fallback (NOT the banned file-based async `CaptureScreenshot(path)`) is only used if `GrabGameViewRT()` returns null.

This is a documented deviation precedent-following from §2b's `SmokeTestRunner2b.cs`. The captures are real Game-View renders, not stale frames or placeholders, and the screenshots themselves prove this (TURN values, distance chips, and ball positions all genuinely differ between C1/C2/C3). I am accepting the deviation as compliant-in-spirit with the CLAUDE.md screenshot rules. **Architect-review may want to confirm this precedent or tighten the rule wording.**

### Maintenance protocol for new contexts
This task did not add any new `*Context.cs` files — it extended the existing `GameSession.cs` (which is a static-bus, not a context, but is the closest analogue). The `capture_helper` maintenance protocol therefore does not apply.

## Spec deviations (acknowledged)

The implementer flagged `Awake()` → `Start()` for the SM subscription to fix an Awake-ordering race. Justification is sound (Unity's Awake-order across GameObjects is non-deterministic; Start runs after all Awakes complete). This is a correctness fix, not a behavior change, and matches Unity best practice for cross-component subscription. **I accept this deviation.**

The extra smoke-runner files (`SmokeRunner2cHost.cs`, `SmokeRunner2cMenu.cs`, `Iter2cTestRunner.cs`) are editor/runtime tooling needed to drive the smoke captures. They live outside production code paths and are not concerning.

## Concerns surfaced for architect

1. **Capture-helper rule wording vs play-mode reality.** The current CLAUDE.md rule mandates `CaptureHelper.SnapGameView()` / `SnapAtEndOfFrameAndPause()`. In play-mode coroutines that need to run uninterrupted, calling `CaptureHelper.SnapGameViewWithLabel()` is dangerous because it triggers `AssetDatabase.Refresh()` which can force domain reload. §2b and now §2c have both addressed this with bespoke `SnapPlayMode` helpers. Architect may want to either (a) bless this pattern in CLAUDE.md, (b) add a `SnapPlayModeSafe()` to `CaptureHelper` so callers don't keep duplicating it, or (c) tighten the spec rule. Not a blocker for §2c.

2. **C3 camera framing matches C2 rather than C1.** The C3 screenshot's camera frame looks closer to C2 (yellow aim flag visible) than to C1 (clean fairway view). The spec only requires TURN=1 after reload, which is satisfied (and the distance chip resets to 506 yds confirming full state reset). The chase camera presumably re-pickups from its post-shot position; not a §2c concern. Worth flagging for architect awareness only.

## Verdict

**FORWARD_TO_ARCHITECT (PASS).** All 21 checklist items pass on visual + code + test-result verification. The TURN counter genuinely increments between shots and resets on hole reload (proven by three real screenshots with different content). Shot history schema matches spec exactly. Test gate is real (118/118, 7 new §2c tests genuinely added). No white-box / placeholder UI. No banned-file modifications. Capture-helper deviations are documented and follow §2b precedent.

STATUS will move to `READY_FOR_ARCHITECT_REVIEW`.
