# STATUS — loop_v2_smoke_bot

**Status:** DONE (Cesar approved 2026-05-20).

Post-review (Cesar-directed, beyond the iter-4b seam-based reviewer pass): the Hole 1
scenario was reworked to genuinely PLAY the hole — real physics shots through the
production `ShotController` drag path (`BeginExternalDrag` → ramped `SetExternalPower` →
`EndExternalDrag`), driver-only first stroke, distance-based club selection, the club
handle visibly pulling down per shot, par+3 `ForceShotComplete` seam fallback. Final run:
3 strokes on Par 5 (Eagle), holed for real. Demo videos for all 3 scenarios recorded and
approved → `Docs/Videos/`. Handover guide → `Docs/Architecture/BOT_FRAMEWORK.md`.
Temporary video-recording scaffolding (`BotVideoRecorder.cs` + hooks) removed.
**Type:** TELLCODE — broader scope than typical (reusable framework, not single-scenario script)
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (inserted between C0 and C1)
**Notion:** Loop v2 Order 335

## Why this exists
Stage C0 unlocked the production playthrough. Stages C1/D/E/F each carry a Cesar visual gate. Without a bot, every gate burns 30+ minutes of manual play across multiple iterations. Bot pays for itself by Stage D.

## History
- 2026-05-19 (initial) — SPEC.md written as a single-scenario Hole 1 playthrough script (8 captures + log).
- 2026-05-19 (revised) — Cesar feedback: bot must drive ANY UI like a real player, not just play-through-to-cup. SPEC rewritten as a two-layer framework:
  - **Driver** (BotDriver.cs) — reusable primitives (Click, WaitForScreen, WaitForModalVisible, FireShot, TypeInto, SetSliderValue, SetToggle, Capture, etc.)
  - **Scenarios** (Scenarios.cs) — thin composable test flows, 30-50 lines each
  - Three scenarios at ship: Hole 1 Playthrough (Stage C1 gate), Settings Round Trip (Stage A surviving flow smoke), Hole Selection Browse (Stage E gate)
  - New scenarios for Stage D/E/F land as additions to Scenarios.cs, not new bot files
- 2026-05-19 (iter-2) — ARCHITECT_REVIEW_FAIL. Addressing: ShellScene contamination, FindCupPosition fix, HoleSelection rework, PNG count SPEC fix, tests-run evidence.
- 2026-05-19 (iter-2 review) — ARCHITECT_REVIEW_FAIL. 5 of 6 iter-1 items resolved (ShellScene, FindCupPosition, BallPosition getter, SPEC DoD 6/4/4, EditMode 305/305). Two persistent FAILs: (a) HoleSelection s02 == s03 byte-identical MD5s — implementer self-graded PASS on contradicting evidence (rubber-stamp failure mode); (b) FireShot/InCup terminal-state observation missing — root cause is polling race + missing §2f scaffolding.
- 2026-05-19 (iter-3) — READY_FOR_ARCHITECT_REVIEW. Both persistent FAILs addressed: (a) HoleSelectionBrowse rewritten to 3-capture honest flow (no more ambiguous CardTapButton); (b) FireShot rewritten with §2f pattern — OnShotComplete subscription fires in 9ms. One remaining FAIL: terminal=AtRest not InCup (result modal absent in s06). §2f fix is complete; AtRest vs InCup is a terrain/preset calibration question for Cesar/architect (see ARCHITECT_REVIEW.md's "informational deferral" option).
- 2026-05-19 (iter-3 review) — ARCHITECT_REVIEW_ESCALATE. Fix 1 (§2f FireShot) and Fix 2 (HoleSelection honest flow) both RESOLVED, verifiable in code + log. ShellScene clean. No new seam expansions. EditMode 305/305 carry-forward. The terminal=AtRest gap is a Cesar judgment call: (A) tighten placement to 30cm to force InCup (relaxes "real player" contract), (B) add `ForceShotCompleteForBot(InCup)` test seam (cleaner; adds one new authorized seam), or (C) accept framework + relabel/drop s06 + defer C1 visual gate to manual play (Stage C1 gate becomes future spec). Reviewer recommendation (non-binding): Option B.
- 2026-05-19 (iter-3 architect call) — Cesar resolved via Option B + codified five-condition seam principle. Verdict file: `ARCHITECT_VERDICT_INCUP.md` (commit `27cecc2f`). Iter-4 fix list: (1) Add `ForceShotCompleteForBot(BallState)` on BallStateMachine, `#if UNITY_EDITOR` guarded, named `_ForBot`; (2) Add `ForceShotComplete(stateName)` primitive to BotDriver — ADDITIONAL to FireShot, not replacement; (3) Revise Hole1Playthrough scenario to call ForceShotComplete("InCup") after s04, capture real result-modal pixels in s06; (4) Update SPEC §"Files POTENTIALLY EDITED" ceiling 2→3 with the five-condition principle pasted verbatim; (5) Update SPEC §DoD hole1_playthrough s06 to require visible modal pixels.
- 2026-05-19/20 (iter-4) — All 5 fix-list code changes landed (seam, primitive, scenario, SPEC). Scenario re-run BLOCKED: 5 consecutive play-mode runs froze at frame=1. Misdiagnosed as Game View visibility; escalated for manual run.
- 2026-05-20 (iter-4b) — Mac was reset; on restart the MCP server (port 21573) had to be relaunched (server is a child process the Unity plugin spawns; it had died overnight). True root cause of the frame=1 freeze found: `PlayerSettings.runInBackground == false` → Unity throttles the play loop to a halt whenever the Editor is not the foreground app (i.e. every headless run). Fix: `Application.runInBackground = true` set at EnteredPlayMode in `LoopV2SmokeBotMenu.cs` — a runtime flag, zero `ProjectSettings.asset` footprint. All 3 scenarios then ran fully headless via MCP. s05/s06 were found to be the same frame (ForceShotComplete skips physics); Cesar's AskUserQuestion call ("Real pre-modal s05") → s05 recaptured from live gameplay before the seam, s06 the modal after — now 100% pixel-distinct. Captures 6/4/3, EditMode 305/305, ShellScene + ProjectSettings clean. → READY_FOR_SELF_REVIEW.
- 2026-05-20 (iter-4b self-review) — SELF_REVIEW_PASS (self-review iteration 1). All checklist items confirmed against fresh captures/logs/code/git. s06 HoleCompleteWidget pixel-verified (✓SUCCESS / Hole 1 - Par 5 / REPLAY / NEXT Hole 2 / PLAY). s05 vs s06 = 100% pixel-diff (independent Pillow check). s04 vs s05 = 2.26% (two honest gameplay frames — within Cesar-accepted range). `ForceShotCompleteForBot` seam: all 5 conditions verified in BallStateMachine.cs:287-315. `runInBackground` fix leaves zero ProjectSettings footprint — `git diff --stat ProjectSettings/` empty, `git diff --stat -- '*.unity'` empty repo-wide. EditMode 305/305. No FAIL items. → golfin-reviewer.
- 2026-05-20 (iter-4b architect review) — ARCHITECT_REVIEW_PASS. Independent pixel scan written before reading any prior verdict. Re-measured pixel-diffs on full-res PNGs: s05↔s06 = 100.00% (distinct screens), s04↔s05 = 1.37% (architect-sanctioned gameplay-frame similarity — resolves the self-reviewer's 2.26% note: self-reviewer measured compressed PNGs, 1.37% full-res is correct, matches implementer's quote). s06 HoleCompleteWidget pixel-verified — C1 gate passes. Seam audit: `ForceShotCompleteForBot` matches ARCHITECT_VERDICT_INCUP.md verbatim, 5-condition compliant, `_ForBot`-suffixed, `#if UNITY_EDITOR` guarded, delegates to `OnShotComplete`; exactly 3 authorized seams, no fourth; `FireShot` still present additionally. Scene-mutation audit: `git diff --stat` empty for `*.unity` and `ProjectSettings/` — `runInBackground` runtime-flag claim holds. Packages/font mods confirmed environmental (MCP plugin 0.72.1→0.72.2 self-update + TMP atlas regen). All 3 history.logs end `=== Scenario complete ===`. EditMode 305/305 evidence file fresh. No FAIL items. → Cesar's final approval.

## Pattern alignment
- Asmdef: `Golfin.Physics.Viewer` (same as §2c-§2f hosts)
- `#if UNITY_EDITOR` guarded throughout
- Four-file pattern: BotDriver.cs (framework) + LoopV2SmokeBot.cs (host) + Scenarios.cs (library) + Editor/LoopV2SmokeBotMenu.cs (launcher)
- Capture path: `CaptureCore.SnapPlayModeSafe` exclusively (no Lesson K traps)
- SessionState armed flag + scenario key + self-destruct + timeScale=1 guard + WaitForSecondsRealtime (all §2f lessons inherited)

## Reusability contract
After this ships, every Loop v2 stage's visual gate includes "add a scenario to `Scenarios.cs` covering the new UI surface; bot must pass before Cesar visual gate." Bot framework is the default acceptance evidence path. Cesar plays manually only when the bot can't reach a flow (rare).
