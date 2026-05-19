# STATUS \u2014 loop_v2_smoke_bot

**Status:** SPEC_READY (architect, 2026-05-19, revised per Cesar feedback)
**Type:** TELLCODE \u2014 broader scope than typical (reusable framework, not single-scenario script)
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (inserted between C0 and C1)
**Notion:** Loop v2 Order 335

## Why this exists
Stage C0 unlocked the production playthrough. Stages C1/D/E/F each carry a Cesar visual gate. Without a bot, every gate burns 30+ minutes of manual play across multiple iterations. Bot pays for itself by Stage D.

## History
- 2026-05-19 (initial) \u2014 SPEC.md written as a single-scenario Hole 1 playthrough script (8 captures + log).
- 2026-05-19 (revised) \u2014 Cesar feedback: bot must drive ANY UI like a real player, not just play-through-to-cup. SPEC rewritten as a two-layer framework:
  - **Driver** (BotDriver.cs) \u2014 reusable primitives (Click, WaitForScreen, WaitForModalVisible, FireShot, TypeInto, SetSliderValue, SetToggle, Capture, etc.)
  - **Scenarios** (Scenarios.cs) \u2014 thin composable test flows, 30-50 lines each
  - Three scenarios at ship: Hole 1 Playthrough (Stage C1 gate), Settings Round Trip (Stage A surviving flow smoke), Hole Selection Browse (Stage E gate)
  - New scenarios for Stage D/E/F land as additions to Scenarios.cs, not new bot files

## Pattern alignment
- Asmdef: `Golfin.Physics.Viewer` (same as \u00a72c-\u00a72f hosts)
- `#if UNITY_EDITOR` guarded throughout
- Four-file pattern: BotDriver.cs (framework) + LoopV2SmokeBot.cs (host) + Scenarios.cs (library) + Editor/LoopV2SmokeBotMenu.cs (launcher)
- Capture path: `CaptureCore.SnapPlayModeSafe` exclusively (no Lesson K traps)
- SessionState armed flag + scenario key + self-destruct + timeScale=1 guard + WaitForSecondsRealtime (all \u00a72f lessons inherited)

## Reusability contract
After this ships, every Loop v2 stage's visual gate includes "add a scenario to `Scenarios.cs` covering the new UI surface; bot must pass before Cesar visual gate." Bot framework is the default acceptance evidence path. Cesar plays manually only when the bot can't reach a flow (rare).
