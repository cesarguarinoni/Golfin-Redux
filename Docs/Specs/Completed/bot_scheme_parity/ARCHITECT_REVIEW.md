# ARCHITECT_REVIEW — `bot_scheme_parity`

**Verdict:** PASS. Built `2ab262c45`, closed `7dd90064d`; folder in Completed, STATUS DONE (Cesar approved 2026-09-06).
**Reviewed:** 2026-09-06 against the commit and the hook source, not the report.

## Verified in the codebase
- One door: `BotSwing.Play` / `PlayPerfect` is the only swing path in `VersusBot`, `PerfBaselineBot` (`ForceFlick`, commented), `TreeOccludeFadeCaptureBot`, `Scenarios` and `ShotSchemeHost`. `IBotSchemeExecutor` + `DriveBot` on all three new drivers; the executors take a delegate context so no `Physics.Viewer` reference cycle.
- Rule 23 hook (`enforce_implementer_done.py` l.2007–2095) greps BOTH the direct calls and the reflection strings (`"BeginExternalDrag"`, `"EndExternalDrag"`, `"CommitFlick"`) — the reflection form is what makes the rule cover `PerfBaselineBot` and editor rigs. Allow-list is explicit with a stated reason per entry. CLAUDE.md rule 17 matches.
- Calibration: `execSigmaPendulum01/Needle01/FreeSwing01` per bracket in `bot_difficulty.csv`; σ solved against the live grader per swing, so the player's equipped club no longer scales bot difficulty (defect 2 in the report — correct fix, it was a real fairness leak). Bracket target E|ErrorYaw| = aimErrorDegMax/2 within 3% across schemes.
- Tree handling: graded difficulty now uses hard trunk rejection under every scheme; the soft preferences survive only for Flick (residual, acceptable — it was the pre-existing behaviour).
- 52/52 live invariants, EditMode 2694/0.

## Raw external-drag callers still in the tree (checked one by one)
| File | Calls | Ruling |
|---|---|---|
| `Scenarios.cs`, `BotDriver.cs` | Begin/End | Allow-listed. `BotDriver` is GRANDFATHERED (loop-v2 smoke determinism) — the hook comment says "tracked in GPS_BACKLOG", but no row existed. **Added the row this review.** |
| `ClubControlArrowDemoRecorder.cs`, `PutterConeSmokeCapture.cs`, `ScreenshotHelper.cs` | Begin → **Cancel** only | Pose-only rigs: they open the Flick aim/timing state to photograph the arrow/cone/gauge and never commit a shot. Not swings; same category as `PowerGaugeMarkerVerifyBot`. Fine as-is. |
| `MapViewCaptureDriver.cs` | Begin → ramp → **End** (l.471–485) | **This one does swing** and is outside the hook's candidate set (not `*Bot.cs` / `*CaptureRig.cs` / `Bot*/`). Under a non-Flick scheme the shot still fires (the seam commits) but the swing animates nothing. Harmless for its purpose (it captures the map view after the ball lands), but it is exactly the class of rig the rule exists for. |

## Follow-up (non-blocking, folded into `scheme_evaluation` kickoff)
1. Widen `_bot_swing_candidate_files` to include `*CaptureDriver.cs`, `*Capture.cs`, `*Recorder.cs` and `Debug/ScreenshotCapture/`, then either migrate `MapViewCaptureDriver` to `BotSwing.Play(ForceFlick)` (one-line, keeps its ramp timing) or allow-list it with a reason. Add the three pose-only rigs to the allow-list with the "Begin→Cancel, never commits" reason so the widened grep stays green. Extend `test_enforce_implementer_done.py` with a `*CaptureDriver.cs` case.

## Needs manual (Cesar) — unchanged from the report
- `PerfBaselineBot` re-baseline on device (numbers should be byte-identical under `ForceFlick`).
- Optional 9-hole strokes runs per scheme to sanity-check the bracket targets against feel.
- Eyeball mid-swing frames: the bot visibly pulls the club / taps the needle / traces the path under each scheme (the invariants assert the state machine, not the pixels).

## Outstanding
Nothing blocks `scheme_evaluation` (Notion 2135).
