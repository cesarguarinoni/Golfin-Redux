# bot_landed_capture_timing_fix

> **Status:** Queued (filed from `spin_and_shot_shape_wiring` close-out, 2026-05-26 10:20 CEST). P3 — Low. Tech-debt.

## One-line

`BotDriver.Capture()` for "landed" frames sometimes captures the post-OB-reset frame (ball back at tee with aim-arc HUD active) instead of the actual at-rest frame, even with iter-2's 2-second settle wait.

## Symptom

`spin_and_shot_shape_wiring` iter-2 visual gate: s06 TOPSPIN and s10 DRAW "landed" stills show ball back at tee with full white aim-arc HUD ("100% Hit FUL" ring), instead of the actual rest position out in the world. Compare with s04 CENTER and s08 BACKSPIN which DO show proper at-rest frames in the foliage / right-edge rough.

s12 FADE shows the same post-reset pattern but is legitimate (FADE went OOB so the reset to tee IS the terminal state).

Bot video + `[Land]`/`[Rest]` log lines were canonical for that task, so this didn't gate the close-out. But for any future visual gate that relies on stills as primary evidence, this is a blocker.

## Hypothesis (rank-ordered)

1. **Capture polls on `[Land]` rather than `[Rest]`**, then the OB-reset path fires between the poll and the screenshot snap, putting the ball back at tee before the GPU frame is captured.
2. **`BallStateMachine` transitions to a non-`AtRest` state when OB-reset is involved** (likely `Aiming` for the next shot) before `WaitForBallAtRest` returns. The wait sees `AtRest` momentarily, then misses the reset.
3. **2-second `WaitForSecondsRealtime` is racing the OB-reset coroutine** — the OB handler may have its own delay that fires after the 2s settle.

## Scope

1. Repro: run `GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate` with verbose state-machine logging enabled. Confirm which hypothesis is correct.
2. Fix: probable solutions in order of preference:
   - Poll `BallStateMachine.State == AtRest` continuously for N frames before capture (debounce against transient states).
   - Capture immediately on `[Rest]` log line, not after a fixed delay.
   - Add explicit "ball is in the world, not at tee" assertion before snapping landed stills (compare ball.position to tee.position).
3. Re-run `SpinAndShapeVisualGate` to verify s06/s10 now show real rest frames.

## Hard rules

- No changes to production code paths (production gameplay loop's OB-reset behavior is correct; this is a bot infra capture-timing bug).
- Bot video continues to be canonical for visual gates per Q5 lock convention.
- `[Land]` and `[Rest]` log emission is unchanged.

## Out of scope

- Per-shot trajectory rendering in stills (a separate visualization feature, not a fix).
- Re-running `spin_and_shot_shape_wiring` — that task is closed; this fix is for future scenarios.
