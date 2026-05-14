# cup_speed_gated_capture — InCup only registers when ball is slow enough

> **STATUS:** Queued (drafted 2026-05-14 by architect chain, surfaced by Cesar Lesson O on `loop_v1_2f_putter_p2_in_context`). **Priority: HIGH — pick up immediately. Live-play correctness bug.**

## One-line

`RealCupDetector` currently registers `InCup` (= hole won) the moment the ball intersects the cup volume, regardless of ball speed. A fast putt that flies over or grazes the cup should NOT count as captured — only a putt whose speed at the cup rim is below a stop-threshold should drop into the hole. Today, any contact wins, which lets you sink at speeds that would physically lip out or skip the cup entirely.

## Cesar's observation (Lesson O, 2026-05-14)

> "Game registers a win when the ball touches the hole regardless of speed."

Cesar flagged this as "might not be for this phase" — confirmed: it's outside §2f scope (§2f is auto-toggle + tuning panel). But the bug is visible in every Lesson O playthrough now that §2f makes putting frequent, so it's queued at HIGH priority for immediate pickup.

## Root cause

[RealCupDetector.cs](Assets/Scripts/Physics/Core/RealCupDetector.cs) — capture path treats geometric overlap with cup volume as sufficient. There's no speed check.

This was acceptable in pre-loop-v1 when putts were tested in isolation at low speeds. Now that the full loop-v1 flow drives players into putter mode automatically and they routinely take strong putts, the bug surfaces every session.

## Scope

1. **Add a speed gate to `RealCupDetector`.** When a ball intersects the cup volume, check `ball.velocity.magnitude` (or fp-equivalent) at the moment of contact. If above threshold, treat as a fly-over (ball continues on its trajectory, possibly lipping the cup edge per existing rim-physics). If below threshold, capture as before.
2. **Threshold determination.** **ARCHITECT-LOCKED 2026-05-14 09:30 JST: 1.5 m/s** sourced from USGA "lip-out" guidance (~5 ft/s at cup rim). Cited via Lesson K methodology in code comments + CSV header. Exposed as new `PuttConfig.Green.CupCaptureSpeed` field for future data-driven tuning. Default value persists at 1.5 m/s until Cesar tunes via DashboardUI or in-loop GreenTuningPanel (P2 follow-up).
3. **Trajectory consequence when speed > threshold.** **ARCHITECT-LOCKED 2026-05-14 09:30 JST: cheap version.** Above-threshold contact does NOT capture; ball continues on existing trajectory uninterrupted. No rim impact damping, no deflection in v1. Rim-physics realism deferred to a future polish task.
4. **Tests:** 3-4 EditMode tests:
   - Putt at 0.5 m/s into cup → captured.
   - Putt at 1.0 m/s into cup → captured (under threshold).
   - Putt at 3.0 m/s into cup → NOT captured (over threshold).
   - Boundary: putt at threshold ± epsilon, deterministic outcome per fp comparison.
5. **Smoke evidence:** capture two contrasting moments:
   - Slow putt into cup → `InCup` modal appears.
   - Fast putt over cup → no modal, ball continues past.
6. **PuttConfig schema bump** if `CupCaptureSpeed` is added: update `putt.csv`, loader, and DashboardUI putt sliders.

## Out of scope

- Rim-physics realism (ball lipping out and curving back). Defer to a future polish task; v1 just doesn't capture above threshold.
- Auto-toggle logic itself (§2f, shipped).
- General cup-volume geometry tuning (separate concern).
- Updating `BallSimulation` putt-phase termination logic beyond what the speed gate requires.

## Hard rules

1. Do NOT change the cup geometry (collider radius, position). This is a velocity gate, not a geometry change.
2. Do NOT modify `BallStateMachine.cs` (Hard Rule 1). If a capture-rejected event is needed for the SM, define a new event on `ICupDetector` and wire it through — don't change SM internals.
3. Real-world data citation per Lesson K: any speed threshold value must cite its source in code comments and CSV headers.
4. Test gate must remain bit-exact pre-existing + N new tests.

## Definition of done

- `RealCupDetector` rejects captures where ball speed at cup-volume entry exceeds threshold.
- Threshold sourced from real-world data, cited in comments + CSV header.
- 3-4 new EditMode tests PASS; baseline+N target met.
- Smoke captures show slow-putt capture vs fast-putt fly-over.
- Cesar Lesson O verification: a fast putt across the cup does NOT register a win.

## Estimate

Half-day to 1 day. Speed gate is small; the time goes into citing the threshold (Lesson K compliance) and writing the boundary tests.

## References

- [RealCupDetector.cs](Assets/Scripts/Physics/Core/RealCupDetector.cs) — the file to modify.
- `Docs/Specs/Completed/loop_v1_2f_putter_p2_in_context/` — Cesar's Lesson O surfaced this.
- `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson K — real-world target citation rule.
- USGA "Stimpmeter" / putt physics literature — anchor for speed threshold (~5 ft/s lip-out).
