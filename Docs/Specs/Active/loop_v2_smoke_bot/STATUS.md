# STATUS — loop_v2_smoke_bot

**Status:** READY_FOR_ARCHITECT_REVIEW
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

## Pattern alignment
- Asmdef: `Golfin.Physics.Viewer` (same as §2c-§2f hosts)
- `#if UNITY_EDITOR` guarded throughout
- Four-file pattern: BotDriver.cs (framework) + LoopV2SmokeBot.cs (host) + Scenarios.cs (library) + Editor/LoopV2SmokeBotMenu.cs (launcher)
- Capture path: `CaptureCore.SnapPlayModeSafe` exclusively (no Lesson K traps)
- SessionState armed flag + scenario key + self-destruct + timeScale=1 guard + WaitForSecondsRealtime (all §2f lessons inherited)

## Reusability contract
After this ships, every Loop v2 stage's visual gate includes "add a scenario to `Scenarios.cs` covering the new UI surface; bot must pass before Cesar visual gate." Bot framework is the default acceptance evidence path. Cesar plays manually only when the bot can't reach a flow (rare).
