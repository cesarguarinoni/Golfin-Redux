# Architect Verdict \u2014 InCup escalation (iter-3)

**Decision:** **Option B with tightened seam principle.**
**Date:** 2026-05-19 18:10 CEST
**For:** Claude Code implementer (iter-4 kickoff)
**Companion docs:** `ARCHITECT_BRIEFING_INCUP.md` (briefing), `ARCHITECT_REVIEW.md` (iter-3 reviewer), `IMPLEMENTER_REPORT.md` (iter-3 grid)

---

## Rationale

The reviewer's option framing is correct. Picking Option B because:

1. **What Stage C1 actually tests is modal wiring.** The parent SPEC (`loop_v2_scope/SPEC.md`) says C1's gate is `HoleCompleteWidget` listens for `BallStateMachine.OnShotComplete` terminal-state-InCup. That's a subscription test, not a physics test. Tying it to "this specific putt sinks on this specific terrain" conflates two different gates and makes Stage C1 hostage to physics calibration we deferred (Phase B Stage 3 NOTES).

2. **Option A breaks the bot's reusability promise.** 30 cm tap-ins are not how players sink putts. Every future scenario that needs InCup evidence (Stage D PLAY NEXT requires a completed hole; Stage E REPLAY needs a previously-cleared hole) would copy this pattern. The 2026-05-19 contract \u2014 *"behaves like a real player"* \u2014 would be a contract in name only by Stage F.

3. **Option C breaks the framework's payoff.** Stage D PLAY NEXT requires a result modal in flight to test the button at all. If C1's modal-visible evidence stays manual, D's is manual too, and E's, and the bot stops being the default acceptance path. The whole point of inserting this task between C0 and C1 was to NOT manually play through every remaining gate.

Option B trades one well-scoped seam for clean coverage of all four remaining stages.

---

## The seam principle (new \u2014 paste into SPEC \u00a7"Files POTENTIALLY EDITED")

A test seam is justified if and only if all five conditions hold:

1. **The seam isolates a real unit of behavior under test.** "Modal subscribes to InCup event" is one unit. "Putt physics produces InCup on Hole 1 green" is a different unit. Conflating them via placement tricks tests neither cleanly.

2. **The production path remains the default for scenarios that genuinely exercise it.** `Fire(preset)` stays the bot's primary shot path. The seam is only for scenarios whose gate is downstream of the terminal state (modal wiring, scene unload, reward grant, progression write).

3. **The seam is `#if UNITY_EDITOR` guarded.** Compiler-level proof it cannot leak into a player build. No "carefully gated at runtime" \u2014 out of the binary entirely.

4. **Production code names the seam for what it is.** Suffix the method `_ForBot` \u2014 e.g. `ForceShotCompleteForBot`. Signals reviewer + future maintainer alike on any grep.

5. **The seam delegates to the same final entry point production uses.** Fire `BallStateMachine.OnShotComplete` with a synthetic `ShotResult { TerminalState = InCup }` \u2014 the SAME event production fires. Modal sees no difference. We're not bypassing the test; we're driving its input deterministically.

The SPEC's two-seam ceiling was a scope-control backstop, not a hard architectural rule. The escalation IS the scope-control mechanism, and we're using it correctly. Ceiling raises 2 \u2192 3 with this principle as the gate for future raises.

---

## Concrete deliverable for iter-4

**(a)** Add a `#if UNITY_EDITOR`-guarded seam. Preferred location: `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` (it's the canonical owner of `OnShotComplete`).

```csharp
#if UNITY_EDITOR
/// <summary>
/// Smoke-bot test seam. Synthesizes a ShotResult and invokes OnShotComplete
/// to drive subscribers (HoleCompleteWidget etc.) deterministically without
/// running physics. ONLY for editor-time bot scenarios where the gate under
/// test is downstream of terminal-state observation (modal wiring, scene
/// unload, reward grant). Production shot path is unchanged.
/// </summary>
public void ForceShotCompleteForBot(BallState terminalState)
{
    var result = new ShotResult { TerminalState = terminalState };
    OnShotComplete?.Invoke(result);
}
#endif
```

Verify the ShotResult constructor + field names match the actual `ShotResult` struct. If it carries additional required fields (Strokes, PenaltyStrokes, etc.), populate sensible defaults; the bot scenario can pass an override if needed via an optional parameter.

**(b)** `BotDriver` gains a **second** shot primitive (does NOT replace `FireShot`):

```csharp
/// <summary>
/// Drives the ball-state machine directly to a terminal state, skipping physics.
/// Use ONLY for scenarios whose gate is downstream of terminal-state observation
/// (modal wiring, scene unload, reward grant, progression write). For scenarios
/// that genuinely test shot mechanics, use FireShot instead.
/// </summary>
public IEnumerator ForceShotComplete(string terminalStateName, float settleSeconds = 0.5f)
{
    LogStep($"ForceShotComplete: driving terminal={terminalStateName}");
    var ctrl = FindFirstObjectByType<PhysicsLabController>();
    if (ctrl == null) { LogStep("ForceShotComplete FAIL: no PhysicsLabController"); yield break; }

    // Parse target state
    if (!System.Enum.TryParse<BallState>(terminalStateName, out var target))
    { LogStep($"ForceShotComplete FAIL: unknown BallState '{terminalStateName}'"); yield break; }

    ctrl.BallSM.ForceShotCompleteForBot(target);
    LogStep($"ForceShotComplete OK: terminal={target}");
    yield return new WaitForSecondsRealtime(settleSeconds);
}
```

Both methods stay in the framework. Scenarios pick the right one for what they're proving.

**(c)** Scenario 1 (`Hole1Playthrough` in `Scenarios.cs`) revised:

- s01..s04 unchanged (Home \u2192 PLAY \u2192 matchmaking \u2192 OPPONENT FOUND \u2192 LabScaffold+Hole_01_Geo loaded \u2192 tee box visible).
- After s04, **drop** the `FireShot(...)` call from this scenario. Replace with `ForceShotComplete("InCup")`.
- Capture s05 = ball-in-cup state (post-event but pre-modal-animate, may show ball at-rest pose since physics didn't drive it; that's accurate \u2014 we're capturing what subscribers see).
- Wait 2s for `HoleCompleteWidget` animate-in.
- Capture s06 = result modal visible.

The bot framework's "real physics" path is still proven by `FireShot` \u2014 keep that primitive in `BotDriver`. A future scenario named `Hole1RealPhysicsShot` (or similar) can call `FireShot` to capture honest shot-mechanics evidence when we want it. For Stage C1's modal-wiring gate, `ForceShotComplete` is the right tool.

**(d)** Update SPEC \u00a7"Files POTENTIALLY EDITED":

- Raise ceiling 2 \u2192 3.
- Add `BallStateMachine.cs` as the third pre-authorized seam (`ForceShotCompleteForBot`).
- Paste the five-condition seam principle (above) verbatim.

**(e)** Update SPEC \u00a7DoD checklist for `hole1_playthrough`:

Replace the "s06 result_modal" line with:
- s05 `ball_in_cup` shows ball-near-cup (terminal-state-driven, no physics required) AND
- s06 `result_modal` shows `HoleCompleteWidget` visible (modal animated in via OnShotComplete subscription).

**(f)** Update `IMPLEMENTER_REPORT.md` iter-4 section:

- Document the architect decision (Option B + seam principle).
- List the three pre-authorized seams used (Matchmaking.Phase + PhysicsLab.BallPosition + BallStateMachine.ForceShotCompleteForBot).
- Mark the original Stage C1 modal-visibility item PASS with pixel evidence from new s06.

**(g)** Re-run all three scenarios. Re-commit captures.

---

## Re-routing

- **STATUS:** ARCHITECT_REVIEW_ESCALATE \u2192 BLOCKED_RESOLVED, route back to implementer (iter-4).
- **Pipeline stage:** golfin-implementer.
- **Self-review + architect-review:** required after iter-4 implementation. Reviewer should specifically check (i) the seam is `#if UNITY_EDITOR` guarded and named `_ForBot`, (ii) `FireShot` primitive is still present in BotDriver (not replaced), (iii) s06 actually shows `HoleCompleteWidget` (pixel scan).

---

## Notes for future bot work

- This decision is the **template** for any future bot seam request: name the unit under test, articulate which condition of the principle justifies the seam, add `_ForBot` suffix, `#if UNITY_EDITOR` guard, delegate to production event.
- Stage D's bot scenario will likely need a similar pattern for testing the MENU button's `UnloadGameplay` path \u2014 same principle applies.
- Stage E's bot scenario (REPLAY) will use `ForceShotComplete("InCup")` to set up a played hole before navigating back to Hole Selection. Same seam, no new seam needed.
- Phase B Stage 3 (real physics calibration) will eventually make a scenario like `Hole1RealPhysicsToCup` work \u2014 at which point we can swap the C1 bot scenario back to using `FireShot`. That's a long way off.
